using System;
using System.Numerics;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;

namespace Andastra.Runtime.Core.Actions
{
    /// <summary>
    /// Action to attack a target entity.
    /// </summary>
    /// <remarks>
    /// Attack Action:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) attack action system
    /// - Located via string references: "EVENT_ON_MELEE_ATTACKED" @ 0x007bccf4, "OnMeleeAttacked" @ 0x007c1a5c
    /// - "ScriptAttacked" @ 0x007bee80 (script hook for attack events)
    /// - Attack-related fields: "AttackList" @ 0x007bf9f0, "AttackID" @ 0x007bfa88, "CurrentAttack" @ 0x007bfa94
    /// - "NumAttacks" @ 0x007bf804, "AttackType" @ 0x007bf914, "AttackResult" @ 0x007bf964
    /// - "RangedAttack" @ 0x007bf8f8, "SneakAttack" @ 0x007bf8ec, "WeaponAttackType" @ 0x007bf8d8
    /// - "AttackMode" @ 0x007bf908, "AttackGroup" @ 0x007bf990, "NewAttackTarget" @ 0x007bfb28
    /// - Attack bonuses: "AttackBonusTable" @ 0x007c2b54, "Base Attack Bonus" @ 0x007c3b44
    /// - "MinAttackBonus" @ 0x007c2f70, "OnHandAttackMod" @ 0x007c2e50, "OffHandAttackMod" @ 0x007c2e2c
    /// - Attack range: "MaxAttackRange" @ 0x007c44e0, "PrefAttackDist" @ 0x007c44d0
    /// - Attack counts: "OnHandAttacks" @ 0x007bfa58, "OffHandAttacks" @ 0x007bfa48, "AdditAttacks" @ 0x007bfa38
    /// - "EffectAttacks" @ 0x007bfa28, "AttackDeflected" @ 0x007bf858, "AttackDebugText" @ 0x007bf83c
    /// - Special attacks: "SpecialAttack" @ 0x007bf9d0, "SpecialAttackId" @ 0x007bf9ac, "SpecAttackList" @ 0x007bf9e0
    /// - "SpecAttackIdList" @ 0x007bf9bc
    /// - Attack animations: "i_attack" @ 0x007c8230, "doneattack01" @ 0x007c8280, "doneattack02" @ 0x007c8270
    /// - "i_attackm" @ 0x007cce0c, "specialattack" @ 0x007cab60, "b_attacked" @ 0x007c2184
    /// - Attack bonus messages:
    ///   - " + %d (Special Attack To Hit Modifier)" @ 0x007c3afc
    ///   - " + %d (Special Attack Damage Bonus)" @ 0x007c3b60
    ///   - " + %d (Special Attack Damage Bonus) (Critical x%d)" @ 0x007c3b84
    ///   - " + %d (Power/Improved Power Attack Damage Bonus)" @ 0x007c3c10
    ///   - " + %d (Power/Improved Power Attack Damage Bonus) (Critical x%d)" @ 0x007c3c48
    ///   - " + %d (Sneak Attack Damage)" @ 0x007c3d20
    /// - GUI: "LBL_ATTACKMOD" @ 0x007cf868, "LBL_ATTACK_INFO" @ 0x007cf8e8
    /// - Error: "CSWClass::LoadAttackBonusTable: Can't load" @ 0x007c4680
    /// - Original implementation: Attack actions trigger combat rounds, fire script events
    /// - Attack resolution uses d20 roll + attack bonus vs target AC
    /// - Natural 20 = automatic hit, natural 1 = automatic miss
    /// - Attack bonuses displayed: " + %d (Base Attack Bonus)" @ 0x007c3b44, " + %d (Effect Attack Bonus)" @ 0x007c39d0
    /// - " + %d (PC Charisma Attack Bonus)" @ 0x007c39ac, " + %d (Offhand Attack Penalty)" @ 0x007c3b24
    /// </remarks>
    public class ActionAttack : ActionBase, IAction
    {
        private readonly uint _targetObjectId;
        private readonly bool _passive;
        private float _attackTimer;
        private const float AttackRange = 2.0f;
        private const float AttackInterval = 2.0f; // Time between attacks

        public int GroupId { get; set; }
        public IEntity Owner { get; set; }

        public ActionAttack(uint targetObjectId, bool passive = false)
            : base(ActionType.AttackObject)
        {
            _targetObjectId = targetObjectId;
            _passive = passive;
            _attackTimer = 0;
        }

        public ActionStatus Update(IEntity actor, float deltaTime)
        {
            return Execute(actor, deltaTime);
        }

        public void Dispose()
        {
            // Cleanup if needed
        }

        protected override ActionStatus ExecuteInternal(IEntity actor, float deltaTime)
        {
            ITransformComponent transform = actor.GetComponent<ITransformComponent>();
            IStatsComponent stats = actor.GetComponent<IStatsComponent>();

            if (transform == null || stats == null)
            {
                return ActionStatus.Failed;
            }

            // Get target entity
            IEntity target = actor.World.GetEntity(_targetObjectId);
            if (target == null || !target.IsValid)
            {
                return ActionStatus.Complete; // Target gone
            }

            ITransformComponent targetTransform = target.GetComponent<ITransformComponent>();
            IStatsComponent targetStats = target.GetComponent<IStatsComponent>();

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
                // Y-up system: Atan2(Y, X) for 2D plane facing
                transform.Facing = (float)Math.Atan2(direction.Y, direction.X);
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

            // Attack calculation based on swkotor2.exe combat system
            // Located via string references: "AttackBonusTable" @ 0x007c2b54, "Base Attack Bonus" @ 0x007c3b44
            // "EVENT_ON_MELEE_ATTACKED" @ 0x007bccf4, "OnMeleeAttacked" @ 0x007c1a5c
            // Original implementation: d20 roll + attack bonus vs target AC
            // Natural 20 = automatic hit (check for critical), natural 1 = automatic miss
            // Attack bonus includes: Base Attack Bonus + Strength/Dexterity modifier + effect bonuses
            // Target AC includes: Base AC + armor + shield + dexterity modifier + effect bonuses
            Random rand = new Random();
            int attackRoll = rand.Next(1, 21);
            int attackBonus = attackerStats.BaseAttackBonus;
            int targetAC = targetStats.ArmorClass;

            // Fire OnPhysicalAttacked script event on target (fires regardless of hit/miss)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): EVENT_ON_MELEE_ATTACKED fires OnMeleeAttacked script
            // Located via string references: "EVENT_ON_MELEE_ATTACKED" @ 0x007bccf4 (case 0xf), "OnMeleeAttacked" @ 0x007c1a5c
            // "ScriptAttacked" @ 0x007bee80 (script ResRef field in creature template)
            // Original implementation: EVENT_ON_MELEE_ATTACKED fires on target when attacked
            // ScriptAttacked script is loaded from creature template and executed when attack occurs
            IEventBus eventBus = attacker.World.EventBus;
            if (eventBus != null)
            {
                eventBus.FireScriptEvent(target, ScriptEvent.OnPhysicalAttacked, attacker);
            }

            // Natural 20 always hits, natural 1 always misses
            if (attackRoll == 20 || (attackRoll != 1 && attackRoll + attackBonus >= targetAC))
            {
                // Hit - deal damage
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Damage calculation includes weapon damage + strength modifier
                // Original engine: Base weapon damage + strength modifier + effect bonuses
                int damage = 1 + rand.Next(0, 8); // 1d8 base damage (simplified - should use weapon damage)
                targetStats.CurrentHP -= damage;

                // Fire damage event
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

                // Fire OnDamaged script event on target
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): CSWSSCRIPTEVENT_EVENTTYPE_ON_DAMAGED fires when entity takes damage
                // Located via string references: "CSWSSCRIPTEVENT_EVENTTYPE_ON_DAMAGED" @ 0x007bcb14 (0x4), "ScriptDamaged" @ 0x007bee70
                // ScriptDamaged script is loaded from creature template and executed when damage is dealt
                // 0x005226d0 @ 0x005226d0 saves ScriptDamaged field, 0x0050c510 @ 0x0050c510 loads ScriptDamaged field
                if (eventBus != null)
                {
                    eventBus.FireScriptEvent(target, ScriptEvent.OnDamaged, attacker);
                }
            }
        }
    }

    /// <summary>
    /// Event fired when damage is dealt.
    /// </summary>
    public class DamageEvent : IGameEvent
    {
        public IEntity Attacker { get; set; }
        public IEntity Target { get; set; }
        public int Damage { get; set; }
        public DamageType DamageType { get; set; }
        public IEntity Entity { get { return Target; } }
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


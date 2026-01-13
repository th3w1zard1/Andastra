using System;
using System.Numerics;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;

namespace Andastra.Runtime.Core.Actions
{
    /// <summary>
    /// Action to bash a locked door until it is destroyed.
    /// </summary>
    /// <remarks>
    /// Bash Door Action:
    /// - Based on swkotor.exe and swkotor2.exe door bashing system
    /// - Located via string references: "gui_mp_bashdp" @ 0x007b5e04, "gui_mp_bashup" @ 0x007b5e14 (swkotor2.exe door bash GUI panels)
    /// - "gui_mp_bashd" @ 0x007b5e24, "gui_mp_bashu" @ 0x007b5e34 (swkotor2.exe door bash GUI elements)
    /// - Original implementation: Repeatedly attempts to break door by applying damage until HP reaches 0
    /// - Each bash attempt: Strength check (d20 + STR modifier vs LockDC), then apply damage if successful
    /// - Bash damage: STR modifier + 1d4 (base bash damage), minimum 1 damage per hit
    /// - Damage is reduced by door Hardness (minimum 1 damage after hardness reduction)
    /// - When door HP <= 0, door is marked as bashed (IsBashed=true), unlocked, and opened
    /// - Door OpenState is set to 2 (destroyed state) when bashed open
    /// - Attack interval: ~2.0 seconds between bash attempts (similar to combat attack intervals)
    /// - Based on swkotor.exe: Door bashing damage application (0x005226d0 @ 0x005226d0 references door bashing)
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Door bashing system (door damage handling in door component)
    /// </remarks>
    public class ActionBashDoor : ActionBase
    {
        private readonly uint _doorObjectId;
        private bool _approached;
        private float _bashTimer;
        private const float InteractRange = 2.0f;
        private const float BashInterval = 2.0f; // Time between bash attempts (seconds)

        public ActionBashDoor(uint doorObjectId)
            : base(ActionType.AttackObject)
        {
            _doorObjectId = doorObjectId;
            _bashTimer = 0f;
        }

        protected override ActionStatus ExecuteInternal(IEntity actor, float deltaTime)
        {
            ITransformComponent transform = actor.GetComponent<ITransformComponent>();
            if (transform == null)
            {
                return ActionStatus.Failed;
            }

            // Get door entity
            IEntity door = actor.World.GetEntity(_doorObjectId);
            if (door == null || !door.IsValid)
            {
                return ActionStatus.Failed;
            }

            IDoorComponent doorComponent = door.GetComponent<IDoorComponent>();
            if (doorComponent == null)
            {
                return ActionStatus.Failed;
            }

            // If door is no longer locked or is already bashed, we're done
            if (!doorComponent.IsLocked || doorComponent.IsBashed)
            {
                return ActionStatus.Complete;
            }

            ITransformComponent doorTransform = door.GetComponent<ITransformComponent>();
            if (doorTransform == null)
            {
                return ActionStatus.Failed;
            }

            Vector3 toTarget = doorTransform.Position - transform.Position;
            toTarget.Y = 0;
            float distance = toTarget.Length();

            // Move towards door if not in range
            if (distance > InteractRange && !_approached)
            {
                IStatsComponent stats = actor.GetComponent<IStatsComponent>();
                float speed = stats != null ? stats.WalkSpeed : 2.5f;

                Vector3 direction = Vector3.Normalize(toTarget);
                float moveDistance = speed * deltaTime;
                float targetDistance = distance - InteractRange;

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

            // Face the door
            if (distance > 0.1f)
            {
                Vector3 direction = Vector3.Normalize(toTarget);
                transform.Facing = (float)Math.Atan2(direction.Y, direction.X);
            }

            // Perform bash attempt at intervals
            _bashTimer += deltaTime;
            if (_bashTimer >= BashInterval)
            {
                _bashTimer = 0f;
                PerformBash(actor, door, doorComponent);
            }

            // Continue until door is destroyed
            if (doorComponent.IsBashed || !doorComponent.IsLocked)
            {
                return ActionStatus.Complete;
            }

            return ActionStatus.InProgress;
        }

        /// <summary>
        /// Performs a single bash attempt on the door.
        /// </summary>
        /// <remarks>
        /// Bash Attempt:
        /// - Based on swkotor.exe and swkotor2.exe door bashing system
        /// - Original implementation: Strength check (d20 + STR modifier vs LockDC)
        /// - If check succeeds: Apply damage (STR modifier + 1d4, minimum 1)
        /// - Damage is reduced by door Hardness (handled by doorComponent.ApplyDamage)
        /// - When HP <= 0, door is automatically marked as bashed, unlocked, and opened
        /// </remarks>
        private void PerformBash(IEntity actor, IEntity door, IDoorComponent doorComponent)
        {
            if (actor == null || door == null || doorComponent == null)
            {
                return;
            }

            // Get actor's strength ability score and modifier
            int strengthScore = 10; // Default
            int strengthModifier = 0;
            IStatsComponent stats = actor.GetComponent<IStatsComponent>();
            if (stats != null)
            {
                strengthScore = stats.GetAbility(Ability.Strength);
                strengthModifier = stats.GetAbilityModifier(Ability.Strength);
            }

            // Roll d20 + STR modifier vs LockDC
            // Based on swkotor.exe: Door bashing strength check
            // Original implementation: Performs strength check before applying damage
            Random random = new Random();
            int roll = random.Next(1, 21); // d20 roll
            int total = roll + strengthModifier;

            // If check succeeds, apply bash damage
            // Based on swkotor.exe: Door bashing damage application
            // Original implementation: Applies damage if strength check succeeds
            if (total >= doorComponent.LockDC)
            {
                // Apply bash damage: STR modifier + 1d4 (base bash damage)
                // Based on swkotor.exe: 0x005226d0 @ 0x005226d0 (door bashing damage calculation)
                // Original implementation: STR modifier + 1d4 base bash damage
                int bashDamage = strengthModifier + random.Next(1, 5); // STR mod + 1d4
                if (bashDamage < 1)
                {
                    bashDamage = 1; // Minimum 1 damage
                }

                // Apply damage to door (handles HP reduction, hardness, and destruction)
                // Based on swkotor.exe and swkotor2.exe: Door bashing damage application
                // Located via string references: Door bashing system
                // Original implementation: ApplyDamage handles HP reduction, hardness reduction, and sets bashed state
                // Hardness reduces damage taken (minimum 1 damage per hit, even if hardness exceeds damage)
                // When CurrentHP <= 0, door is marked as bashed (IsBashed=true), unlocked, and opened
                // OpenState is set to 2 (destroyed state) when door is bashed open
                doorComponent.ApplyDamage(bashDamage);

                Console.WriteLine("[ActionBashDoor] Bashed door: roll=" + roll + " + STR mod=" + strengthModifier + " = " + total + " vs DC=" + doorComponent.LockDC + ", damage=" + bashDamage + ", door HP=" + doorComponent.HitPoints);

                // Fire OnDamaged script event if door still exists (HP > 0 means door wasn't destroyed)
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): CSWSSCRIPTEVENT_EVENTTYPE_ON_DAMAGED fires when entity takes damage
                // Located via string references: "CSWSSCRIPTEVENT_EVENTTYPE_ON_DAMAGED" @ 0x007bcb14 (0x4), "ScriptDamaged" @ 0x007bee70
                if (doorComponent.HitPoints > 0 && actor.World != null && actor.World.EventBus != null)
                {
                    actor.World.EventBus.FireScriptEvent(door, ScriptEvent.OnDamaged, actor);
                }

                // If door was destroyed (bashed open), it's already unlocked and opened by ApplyDamage
                if (doorComponent.IsBashed)
                {
                    Console.WriteLine("[ActionBashDoor] Door bashed open!");

                    // Fire OnDeath script event (door destruction)
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): CSWSSCRIPTEVENT_EVENTTYPE_ON_DEATH fires when entity dies/is destroyed
                    // Located via string references: "CSWSSCRIPTEVENT_EVENTTYPE_ON_DEATH" @ 0x007bcaf0 (0x3)
                    if (actor.World != null && actor.World.EventBus != null)
                    {
                        actor.World.EventBus.FireScriptEvent(door, ScriptEvent.OnDeath, actor);
                    }
                }
            }
            else
            {
                Console.WriteLine("[ActionBashDoor] Bash attempt failed: roll=" + roll + " + STR mod=" + strengthModifier + " = " + total + " < DC=" + doorComponent.LockDC);
            }
        }
    }
}


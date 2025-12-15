using System;
using System.Numerics;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;

namespace Odyssey.Core.Actions
{
    /// <summary>
    /// Skill constants for KOTOR.
    /// </summary>
    public static class SkillConstants
    {
        public const int SKILL_COMPUTER_USE = 0;
        public const int SKILL_DEMOLITIONS = 1;
        public const int SKILL_STEALTH = 2;
        public const int SKILL_AWARENESS = 3;
        public const int SKILL_PERSUADE = 4;
        public const int SKILL_REPAIR = 5;
        public const int SKILL_SECURITY = 6;
        public const int SKILL_TREAT_INJURY = 7;
    }

    /// <summary>
    /// Action to use/interact with an object (door, placeable, etc.).
    /// Based on NWScript ActionUseObject semantics.
    /// </summary>
    /// <remarks>
    /// Use Object Action:
    /// - Based on swkotor2.exe object interaction system
    /// - Original implementation: Moves actor to use point (BWM hook vector), faces target, triggers OnUsed script
    /// - Use distance: ~2.0 units (configurable per object type)
    /// - Objects can specify use point via BWM hook vectors (for doors, placeables)
    /// - Script events: OnUsed (placeables), OnOpen/OnClose (doors), OnClick (triggers)
    /// </remarks>
    public class ActionUseObject : ActionBase
    {
        private readonly uint _targetObjectId;
        private const float UseDistance = 2.0f;
        private bool _reachedTarget;
        private bool _hasUsed;
        private static readonly Random _random = new Random();

        public ActionUseObject(uint targetObjectId)
            : base(ActionType.UseObject)
        {
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

            // Get use point (could be from BWM hooks, but for now use object position)
            Vector3 usePoint = GetUsePoint(target, targetTransform);

            Vector3 toTarget = usePoint - transform.Position;
            toTarget.Y = 0; // Ignore vertical
            float distance = toTarget.Length();

            // Move to use point first
            if (distance > UseDistance)
            {
                if (!_reachedTarget)
                {
                    // Move towards target
                    IStatsComponent stats = actor.GetComponent<IStatsComponent>();
                    float speed = stats != null ? stats.RunSpeed : 5.0f;

                    Vector3 direction = Vector3.Normalize(toTarget);
                    float moveDistance = speed * deltaTime;

                    if (moveDistance > distance - UseDistance)
                    {
                        moveDistance = distance - UseDistance;
                    }

                    transform.Position += direction * moveDistance;
                    // Y-up system: Atan2(Y, X) for 2D plane facing
                    transform.Facing = (float)Math.Atan2(direction.Y, direction.X);
                }
                return ActionStatus.InProgress;
            }

            // Reached target - face it and use it
            if (!_reachedTarget)
            {
                _reachedTarget = true;
                Vector3 direction = Vector3.Normalize(toTarget);
                // Y-up system: Atan2(Y, X) for 2D plane facing
                transform.Facing = (float)Math.Atan2(direction.Y, direction.X);
            }

            // Execute use logic
            if (!_hasUsed)
            {
                _hasUsed = true;

                // Fire OnUsed script event
                IScriptHooksComponent scriptHooks = target.GetComponent<IScriptHooksComponent>();
                if (scriptHooks != null)
                {
                    string scriptResRef = scriptHooks.GetScript(ScriptEvent.OnUsed);
                    if (!string.IsNullOrEmpty(scriptResRef))
                    {
                        // Fire script event via world event bus
                        IEventBus eventBus = actor.World.EventBus;
                        if (eventBus != null)
                        {
                            eventBus.FireScriptEvent(target, ScriptEvent.OnUsed, actor);
                        }
                    }
                }

                // Handle door/placeable specific logic
                IDoorComponent door = target.GetComponent<IDoorComponent>();
                if (door != null)
                {
                    if (!door.IsOpen)
                    {
                        // Try to open door
                        if (door.IsLocked)
                        {
                            // Check if actor has the key
                            if (door.KeyRequired && !string.IsNullOrEmpty(door.KeyTag))
                            {
                                IInventoryComponent inventory = actor.GetComponent<IInventoryComponent>();
                                if (inventory != null && inventory.HasItemByTag(door.KeyTag))
                                {
                                    door.Unlock();
                                }
                                else
                                {
                                    // Actor doesn't have the required key
                                    return ActionStatus.Failed;
                                }
                            }

                            // If lockable by script and has lock DC, attempt to unlock
                            if (door.LockableByScript && door.LockDC > 0)
                            {
                                // Roll skill check (Security) vs LockDC
                                if (!PerformSkillCheck(actor, SkillConstants.SKILL_SECURITY, door.LockDC))
                                {
                                    // Skill check failed - cannot unlock
                                    return ActionStatus.Failed;
                                }
                                // Skill check succeeded - unlock the door
                                door.Unlock();
                            }

                            // If just locked without key requirement, fail
                            return ActionStatus.Failed;
                        }
                        door.Open();
                    }
                }

                IPlaceableComponent placeable = target.GetComponent<IPlaceableComponent>();
                if (placeable != null)
                {
                    if (!placeable.IsUseable)
                    {
                        return ActionStatus.Failed;
                    }

                    // Handle locked placeables
                    if (placeable.IsLocked)
                    {
                        // Check if actor has the key
                        if (!string.IsNullOrEmpty(placeable.KeyTag))
                        {
                            IInventoryComponent inventory = actor.GetComponent<IInventoryComponent>();
                            if (inventory != null && inventory.HasItemByTag(placeable.KeyTag))
                            {
                                placeable.Unlock();
                            }
                            else
                            {
                                // Actor doesn't have the required key
                                return ActionStatus.Failed;
                            }
                        }

                        // If has lock DC, attempt to unlock
                        if (placeable.LockDC > 0)
                        {
                            // Roll skill check (Security) vs LockDC
                            if (!PerformSkillCheck(actor, SkillConstants.SKILL_SECURITY, placeable.LockDC))
                            {
                                // Skill check failed - cannot unlock
                                return ActionStatus.Failed;
                            }
                            // Skill check succeeded - unlock the placeable
                            placeable.Unlock();
                        }

                        return ActionStatus.Failed;
                    }

                    // Handle placeable use
                    if (placeable.HasInventory)
                    {
                        // Container - open it
                        if (!placeable.IsOpen)
                        {
                            placeable.Open();
                        }
                    }
                    else
                    {
                        // Regular placeable - activate it
                        placeable.Activate();
                    }
                }
            }

            return ActionStatus.Complete;
        }

        /// <summary>
        /// Performs a skill check (d20 + skill rank + ability modifier) vs DC.
        /// </summary>
        /// <param name="actor">The entity performing the skill check</param>
        /// <param name="skill">Skill ID (SKILL_SECURITY, etc.)</param>
        /// <param name="dc">Difficulty class to beat</param>
        /// <returns>True if skill check succeeds, false otherwise</returns>
        private bool PerformSkillCheck(IEntity actor, int skill, int dc)
        {
            IStatsComponent stats = actor.GetComponent<IStatsComponent>();
            if (stats == null)
            {
                return false; // No stats component = cannot perform skill check
            }

            // Get skill rank
            int skillRank = stats.GetSkillRank(skill);
            if (skillRank < 0)
            {
                return false; // Skill doesn't exist for this entity
            }

            // Roll d20
            int roll = _random.Next(1, 21);

            // Calculate total: d20 roll + skill rank
            // Note: In full implementation, we'd also add ability modifier (DEX for Security)
            // For now, just use skill rank
            int total = roll + skillRank;

            // Check if total meets or exceeds DC
            return total >= dc;
        }

        private Vector3 GetUsePoint(IEntity target, ITransformComponent targetTransform)
        {
            // TODO: Get from BWM hooks (USE1/USE2) if available
            // For now, use object position
            return targetTransform.Position;
        }
    }
}


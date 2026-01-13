using System;
using Andastra.Runtime.Core.Combat;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Games.Common;
using Andastra.Game.Games.Common;

namespace Andastra.Runtime.Core.AI
{
    /// <summary>
    /// Legacy AI controller wrapper for Runtime.Core compatibility.
    /// Wraps the unified AIControllerSystem for use in World.cs and other Runtime.Core code.
    /// </summary>
    /// <remarks>
    /// Legacy AI Controller:
    /// - Maintains backward compatibility with Runtime.Core.World class
    /// - Uses unified AIControllerSystem internally with EngineFamily.Unknown (default behavior)
    /// - Wraps EventBus.FireScriptEvent for script event firing
    /// - This class is kept for backward compatibility but delegates to AIControllerSystem
    /// - Original implementation merged into Runtime.Games.Common.AIControllerSystem
    /// </remarks>
    public class AIController : BaseAIControllerSystem
    {
        private readonly AIControllerSystem _aiControllerSystem;

        public AIController(IWorld world, CombatSystem combatSystem)
            : base(world)
        {
            if (combatSystem == null)
            {
                throw new ArgumentNullException(nameof(combatSystem));
            }

            // Create unified AIControllerSystem with EngineFamily.Unknown (default behavior)
            // Fire script events via EventBus
            _aiControllerSystem = new AIControllerSystem(
                world,
                EngineFamily.Unknown,
                (entity, scriptEvent, target) =>
                {
                    if (world.EventBus != null)
                    {
                        world.EventBus.FireScriptEvent(entity, scriptEvent, target);
                    }
                });
        }

        /// <summary>
        /// Updates AI for all NPCs.
        /// </summary>
        /// <param name="deltaTime">Time since last frame in seconds.</param>
        public override void Update(float deltaTime)
        {
            // Delegate to unified system
            _aiControllerSystem.Update(deltaTime);
        }

        /// <summary>
        /// Fires heartbeat script for a creature.
        /// Implemented for abstract method requirement - actual work done by unified system.
        /// </summary>
        protected override void FireHeartbeatScript(IEntity creature)
        {
            // Implementation not needed - unified system handles this via Update()
            // This is only here to satisfy abstract method requirement
        }

        /// <summary>
        /// Checks perception for a creature.
        /// Implemented for abstract method requirement - actual work done by unified system.
        /// </summary>
        protected override void CheckPerception(IEntity creature)
        {
            // Implementation not needed - unified system handles this via Update()
            // This is only here to satisfy abstract method requirement
        }

        /// <summary>
        /// Handles combat AI for a creature.
        /// Implemented for abstract method requirement - actual work done by unified system.
        /// </summary>
        protected override void HandleCombatAI(IEntity creature)
        {
            // Implementation not needed - unified system handles this via Update()
            // This is only here to satisfy abstract method requirement
        }

        /// <summary>
        /// Clears AI state for an entity (when destroyed).
        /// Legacy method for backward compatibility.
        /// </summary>
        public void ClearAIState(IEntity entity)
        {
            // Delegate to unified system
            _aiControllerSystem.OnEntityDestroyed(entity);
        }
    }
}


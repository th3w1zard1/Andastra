using System;
using System.Collections.Generic;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Common
{
    /// <summary>
    /// Base implementation of AI management shared across all BioWare engines.
    /// </summary>
    /// <remarks>
    /// Base AI Manager Implementation:
    /// - Common AI behavior framework across all engines
    /// - Handles perception, decision making, action execution
    /// - Provides foundation for engine-specific AI behaviors
    ///
    /// Based on reverse engineering of:
    /// - swkotor.exe: Basic AI with perception and combat
    /// - swkotor2.exe: Advanced AI with influence, factions, complex behaviors
    /// - nwmain.exe: D20-based AI with tactical positioning
    /// - daorigins.exe: Complex tactical AI with positioning and abilities
    /// - DragonAge2.exe: Enhanced Eclipse engine AI with improved combat behavior and party tactics
    ///   - Located via string references: "PackageAI" @ 0x00bf468c, "SetBehaviourMessage" @ 0x00bfc8b4
    ///   - Combat system: "CombatTarget" @ 0x00bf4dc0, "InCombat" @ 0x00bf4c10, "Combatant" @ 0x00bf4664
    ///   - Game mode: "GameModeCombat" @ 0x00beaf3c, "BInCombatMode" @ 0x00beeed2
    ///   - Behavior system: "SetBehaviourMessage" @ 0x00bfc8b4 (UnrealScript message passing)
    ///   - Enhanced from daorigins.exe: Improved party coordination, better ability selection, refined positioning
    ///   - Uses same Eclipse engine architecture as daorigins.exe: UnrealScript event system, navigation mesh pathfinding
    ///   - AI update loop: Integrated with Unreal Engine 3 frame update system
    ///   - Perception system: Enhanced detection with improved line-of-sight calculations
    ///   - Combat AI: More sophisticated tactical decision-making with party role awareness
    /// - Common AI concepts: Perception, decision trees, action queues, behavior states
    ///
    /// Common functionality across engines:
    /// - Perception system (sight, hearing, detection)
    /// - Behavior state management (idle, combat, alert, etc.)
    /// - Action queue management and execution
    /// - Target selection and threat assessment
    /// - Group coordination and tactics
    /// - Script integration for custom behaviors
    /// - Performance considerations for large numbers of AI entities
    /// </remarks>
    [PublicAPI]
    public abstract class BaseAIManager
    {
        protected readonly IWorld _world;
        protected readonly Dictionary<uint, AIController> _aiControllers = new Dictionary<uint, AIController>();

        protected BaseAIManager(IWorld world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        /// <summary>
        /// Registers an entity for AI control.
        /// </summary>
        /// <remarks>
        /// Sets up AI controller for the entity.
        /// Initializes perception, behavior state, action queue.
        /// Common across all engines.
        /// </remarks>
        public virtual void RegisterEntity(IEntity entity)
        {
            if (entity == null || _aiControllers.ContainsKey(entity.ObjectId))
                return;

            var controller = CreateAIController(entity);
            _aiControllers[entity.ObjectId] = controller;
            OnEntityRegistered(entity, controller);
        }

        /// <summary>
        /// Unregisters an entity from AI control.
        /// </summary>
        /// <remarks>
        /// Cleans up AI controller and resources.
        /// Called when entity is destroyed or removed.
        /// Common across all engines.
        /// </remarks>
        public virtual void UnregisterEntity(IEntity entity)
        {
            if (entity == null || !_aiControllers.TryGetValue(entity.ObjectId, out var controller))
                return;

            OnEntityUnregistering(entity, controller);
            _aiControllers.Remove(entity.ObjectId);
        }

        /// <summary>
        /// Updates all AI controllers.
        /// </summary>
        /// <remarks>
        /// Called each frame to update AI state.
        /// Handles perception updates, decision making, action execution.
        /// Performance-critical - must scale with entity count.
        /// </remarks>
        public virtual void Update(float deltaTime)
        {
            foreach (var controller in _aiControllers.Values)
            {
                if (controller.Entity.IsValid)
                {
                    controller.Update(deltaTime);
                }
            }
        }

        /// <summary>
        /// Forces an AI controller to reevaluate its situation.
        /// </summary>
        /// <remarks>
        /// Triggers immediate perception update and decision making.
        /// Used when environment changes significantly.
        /// Common across all engines.
        /// </remarks>
        public virtual void ForceReevaluation(IEntity entity)
        {
            if (entity != null && _aiControllers.TryGetValue(entity.ObjectId, out var controller))
            {
                controller.ForceReevaluation();
            }
        }

        /// <summary>
        /// Sets the behavior state for an AI entity.
        /// </summary>
        /// <remarks>
        /// Changes AI behavior mode (idle, combat, alert, etc.).
        /// Triggers appropriate behavior transitions.
        /// Engine-specific states may be available.
        /// </remarks>
        public virtual void SetBehaviorState(IEntity entity, AIBehaviorState state)
        {
            if (entity != null && _aiControllers.TryGetValue(entity.ObjectId, out var controller))
            {
                controller.SetBehaviorState(state);
            }
        }

        /// <summary>
        /// Gets the current behavior state of an AI entity.
        /// </summary>
        /// <remarks>
        /// Returns current AI state for debugging or scripting.
        /// Common across all engines.
        /// </remarks>
        public virtual AIBehaviorState GetBehaviorState(IEntity entity)
        {
            if (entity != null && _aiControllers.TryGetValue(entity.ObjectId, out var controller))
            {
                return controller.CurrentState;
            }
            return AIBehaviorState.Idle;
        }

        /// <summary>
        /// Creates an AI controller for an entity.
        /// </summary>
        /// <remarks>
        /// Factory method for engine-specific AI controller creation.
        /// Subclasses implement engine-specific AI logic.
        /// </remarks>
        protected abstract AIController CreateAIController(IEntity entity);

        /// <summary>
        /// Called when an entity is registered.
        /// </summary>
        /// <remarks>
        /// Hook for engine-specific registration logic.
        /// Subclasses can override for additional setup.
        /// </remarks>
        protected virtual void OnEntityRegistered(IEntity entity, AIController controller)
        {
            // Default: no additional logic
        }

        /// <summary>
        /// Called when an entity is being unregistered.
        /// </summary>
        /// <remarks>
        /// Hook for engine-specific cleanup logic.
        /// Subclasses can override for additional cleanup.
        /// </remarks>
        protected virtual void OnEntityUnregistering(IEntity entity, AIController controller)
        {
            // Default: no additional logic
        }

        /// <summary>
        /// Gets AI system statistics.
        /// </summary>
        /// <remarks>
        /// Returns AI metrics for debugging and performance monitoring.
        /// Useful for optimizing AI performance.
        /// </remarks>
        public virtual AIStats GetStats()
        {
            return new AIStats
            {
                RegisteredEntityCount = _aiControllers.Count,
                ActiveControllerCount = _aiControllers.Count // All registered are active
            };
        }
    }

    /// <summary>
    /// AI controller for individual entities.
    /// </summary>
    public abstract class AIController
    {
        /// <summary>
        /// The entity this controller manages.
        /// </summary>
        public IEntity Entity { get; }

        /// <summary>
        /// Current behavior state.
        /// </summary>
        public AIBehaviorState CurrentState { get; protected set; }

        /// <summary>
        /// Target entity (if any).
        /// </summary>
        public IEntity Target { get; protected set; }

        protected AIController(IEntity entity)
        {
            Entity = entity ?? throw new ArgumentNullException(nameof(entity));
            CurrentState = AIBehaviorState.Idle;
        }

        /// <summary>
        /// Updates the AI controller.
        /// </summary>
        /// <remarks>
        /// Main AI update loop.
        /// Handles perception, decision making, action execution.
        /// Called each frame by AI manager.
        /// </remarks>
        public abstract void Update(float deltaTime);

        /// <summary>
        /// Forces immediate reevaluation.
        /// </summary>
        /// <remarks>
        /// Triggers perception update and decision making.
        /// Used when environment changes.
        /// </remarks>
        public abstract void ForceReevaluation();

        /// <summary>
        /// Sets the behavior state.
        /// </summary>
        /// <remarks>
        /// Changes AI behavior mode.
        /// Triggers state transition logic.
        /// </remarks>
        public abstract void SetBehaviorState(AIBehaviorState state);

        /// <summary>
        /// Sets the target entity.
        /// </summary>
        /// <remarks>
        /// Updates focus target for AI behaviors.
        /// May trigger target-related behaviors.
        /// </remarks>
        public virtual void SetTarget(IEntity target)
        {
            Target = target;
        }

        /// <summary>
        /// Clears the current target.
        /// </summary>
        /// <remarks>
        /// Removes target focus.
        /// May trigger idle or search behaviors.
        /// </remarks>
        public virtual void ClearTarget()
        {
            Target = null;
        }
    }

    /// <summary>
    /// AI behavior states.
    /// </summary>
    public enum AIBehaviorState
    {
        /// <summary>
        /// Idle, no specific behavior.
        /// </summary>
        Idle,

        /// <summary>
        /// Patrolling or wandering.
        /// </summary>
        Patrol,

        /// <summary>
        /// Investigating disturbance.
        /// </summary>
        Investigate,

        /// <summary>
        /// Alert and searching.
        /// </summary>
        Alert,

        /// <summary>
        /// In combat.
        /// </summary>
        Combat,

        /// <summary>
        /// Fleeing from threat.
        /// </summary>
        Flee,

        /// <summary>
        /// Following leader.
        /// </summary>
        Follow,

        /// <summary>
        /// Guarding position.
        /// </summary>
        Guard,

        /// <summary>
        /// Custom script-controlled behavior.
        /// </summary>
        Script
    }

    /// <summary>
    /// AI system statistics.
    /// </summary>
    public struct AIStats
    {
        /// <summary>
        /// Number of registered entities.
        /// </summary>
        public int RegisteredEntityCount;

        /// <summary>
        /// Number of active AI controllers.
        /// </summary>
        public int ActiveControllerCount;
    }
}

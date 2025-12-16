using System;
using Odyssey.Core.Actions;
using Odyssey.Core.AI;
using Odyssey.Core.Combat;
using Odyssey.Core.Dialogue;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;
using Odyssey.Core.Perception;
using Odyssey.Core.Triggers;

namespace Odyssey.Core.GameLoop
{
    /// <summary>
    /// Fixed-timestep game loop implementation following the 7-phase pattern.
    /// </summary>
    /// <remarks>
    /// Fixed-Timestep Game Loop:
    /// - Based on swkotor2.exe game loop implementation
    /// - Located via string references: Game loop runs at 60 Hz fixed timestep for deterministic simulation
    /// - Original implementation: Fixed timestep ensures deterministic behavior for scripts, combat, AI
    /// - Game loop phases (executed in order):
    ///   1. Input Phase - Collect input events, update camera, handle click-to-move
    ///   2. Script Phase - Process delay wheel, fire heartbeats, execute action queues (budget-limited)
    ///   3. Simulation Phase - Update entity positions, perception checks, combat rounds
    ///   4. Animation Phase - Advance skeletal animations, particle emitters, lip sync
    ///   5. Scene Sync Phase - Sync runtime transforms → MonoGame rendering structures
    ///   6. Render Phase - Cull by VIS groups, sort transparency, submit draw calls
    ///   7. Audio Phase - Update spatial audio positions, trigger one-shots
    /// - Fixed timestep: 1/60 seconds (60 Hz simulation)
    /// - Max frame time cap: 0.25 seconds to prevent spiral of death
    /// - Script budget: Max 1000 instructions per frame to prevent lockups
    /// - Interpolation: Render phase uses interpolation factor for smooth rendering between fixed updates
    /// </remarks>
    public class FixedTimestepGameLoop
    {
        private const float FixedTimestep = 1f / 60f;  // 60 Hz simulation
        private const float MaxFrameTime = 0.25f;      // Cap to prevent spiral of death
        private const int MaxScriptBudget = 1000;       // Max instructions per frame

        private readonly IWorld _world;
        private float _accumulator;
        private float _simulationTime;

        public FixedTimestepGameLoop(IWorld world)
        {
            _world = world ?? throw new ArgumentNullException("world");
            _accumulator = 0f;
            _simulationTime = 0f;
        }

        /// <summary>
        /// Updates the game loop with variable frame time.
        /// </summary>
        /// <param name="frameTime">Time elapsed since last frame in seconds.</param>
        public void Update(float frameTime)
        {
            // Cap frame time to prevent spiral of death
            frameTime = Math.Min(frameTime, MaxFrameTime);

            _accumulator += frameTime;

            // Fixed-timestep simulation
            while (_accumulator >= FixedTimestep)
            {
                FixedUpdate(FixedTimestep);
                _simulationTime += FixedTimestep;
                _accumulator -= FixedTimestep;
            }
        }

        /// <summary>
        /// Gets the interpolation factor for smooth rendering.
        /// </summary>
        public float GetInterpolationAlpha()
        {
            return _accumulator / FixedTimestep;
        }

        /// <summary>
        /// Gets the current simulation time.
        /// </summary>
        public float SimulationTime
        {
            get { return _simulationTime; }
        }

        /// <summary>
        /// Fixed-timestep update following the 7-phase pattern.
        /// </summary>
        /// <param name="dt">Fixed timestep (1/60 seconds).</param>
        private void FixedUpdate(float dt)
        {
            // Phase 1: Input Phase (handled by MonoGame Update loop, not here)

            // Phase 2: Script Phase
            // Process delayed commands (DelayCommand wheel)
            if (_world.DelayScheduler != null)
            {
                _world.DelayScheduler.Update(dt);
            }

            // Process heartbeats (every 6 seconds, handled by AI controller)
            // Heartbeat processing is done in AI controller Update()

            // Process action queues (budget-limited)
            int instructionsUsed = 0;
            foreach (IEntity entity in _world.GetAllEntities())
            {
                if (instructionsUsed >= MaxScriptBudget)
                {
                    break;
                }

                IActionQueueComponent actionQueue = entity.GetComponent<IActionQueueComponent>();
                if (actionQueue != null)
                {
                    actionQueue.Process(dt);
                    // TODO: Track instruction count from script execution
                    // instructionsUsed += actionQueue.GetInstructionCount();
                }
            }

            // Phase 3: Simulation Phase
            // Update perception (every 0.5 seconds, staggered)
            if (_world.PerceptionSystem != null)
            {
                _world.PerceptionSystem.Update(dt);
            }

            // Update combat rounds
            if (_world.CombatSystem != null)
            {
                _world.CombatSystem.Update(dt);
            }

            // Update AI
            if (_world.AIController != null)
            {
                _world.AIController.Update(dt);
            }

            // Update triggers
            if (_world.TriggerSystem != null)
            {
                _world.TriggerSystem.Update(dt);
            }

            // Update world (time manager, event bus)
            _world.Update(dt);

            // Phase 4: Animation Phase (handled by MonoGame rendering, not here)

            // Phase 5: Scene Sync Phase (handled by MonoGame rendering, not here)

            // Phase 6: Render Phase (handled by MonoGame Draw loop, not here)

            // Phase 7: Audio Phase (handled by MonoGame audio system, not here)
        }
    }
}


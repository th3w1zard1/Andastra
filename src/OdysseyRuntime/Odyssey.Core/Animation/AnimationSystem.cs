using System;
using System.Collections.Generic;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;

namespace Odyssey.Core.Animation
{
    /// <summary>
    /// System that updates animations for all entities with animation components.
    /// </summary>
    /// <remarks>
    /// Animation System:
    /// - Based on swkotor2.exe animation system
    /// - Located via string references: "Animation" @ 0x007bf604, "AnimList" @ 0x007c3694, "AnimationTime" @ 0x007bf810
    /// - "AnimationLength" @ 0x007bf980, "AnimationState" @ 0x007c1f30, "Animations" @ 0x007c4e38
    /// - "CombatAnimations" @ 0x007c4ea4, "DialogAnimations" @ 0x007c4eb8 (animation categories)
    /// - "PlayAnim" @ 0x007c346c, "AnimLoop" @ 0x007c4c70 (animation loop flag)
    /// - "CurrentAnim" @ 0x007c38d4, "NextAnim" @ 0x007c38c8 (animation state tracking)
    /// - "LookAtAnimation" @ 0x007bb4e0, "ReaxnAnimation" @ 0x007bf93c, "CameraAnimation" @ 0x007c3460
    /// - "EVENT_PLAY_ANIMATION" @ 0x007bcd74 (animation event type)
    /// - Animation timing: "frameStart" @ 0x007ba698, "frameEnd" @ 0x007ba668 (animation frame timing)
    /// - Skinned animation shader: Vertex program for skinned animations @ 0x0081c228, 0x0081fe20 (GPU skinning)
    /// - Error messages: "CSWCAnimBasePlaceable::ServerToClientAnimation(): Failed to map server anim %i to client anim." @ 0x007d2330
    /// - "CSWCAnimBaseDoor::GetAnimationName(): No name for server animation %d" @ 0x007d24a8
    /// - Original implementation: Updates animation time for all entities each frame
    /// - Animation system advances animation time based on deltaTime and AnimationSpeed
    /// - Non-looping animations complete when AnimationTime reaches animation duration
    /// - Looping animations wrap AnimationTime back to 0.0 when reaching duration
    /// - Animation durations are typically stored in MDX animation data (not tracked here, assumed infinite for now)
    /// - Animation completion events could be fired here (not implemented yet)
    /// </remarks>
    public class AnimationSystem
    {
        private readonly IWorld _world;
        private const float DefaultAnimationDuration = 1.0f; // Placeholder - should come from MDX data

        public AnimationSystem(IWorld world)
        {
            _world = world ?? throw new ArgumentNullException("world");
        }

        /// <summary>
        /// Updates all animations in the world.
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last update.</param>
        public void Update(float deltaTime)
        {
            if (_world == null)
            {
                return;
            }

            // Iterate over all entities with animation components
            foreach (IEntity entity in _world.GetAllEntities())
            {
                IAnimationComponent animation = entity.GetComponent<IAnimationComponent>();
                if (animation == null)
                {
                    continue;
                }

                // Skip if no animation is playing
                if (animation.CurrentAnimation < 0)
                {
                    continue;
                }

                // Update animation time
                float newTime = animation.AnimationTime + (deltaTime * animation.AnimationSpeed);

                // Handle animation completion
                if (!animation.IsLooping)
                {
                    // For non-looping animations, clamp to duration and mark complete
                    if (newTime >= DefaultAnimationDuration)
                    {
                        animation.AnimationTime = DefaultAnimationDuration;
                        // AnimationComplete is computed property, will be true now
                    }
                    else
                    {
                        animation.AnimationTime = newTime;
                    }
                }
                else
                {
                    // For looping animations, wrap time
                    if (newTime >= DefaultAnimationDuration)
                    {
                        animation.AnimationTime = newTime % DefaultAnimationDuration;
                    }
                    else
                    {
                        animation.AnimationTime = newTime;
                    }
                }
            }
        }
    }
}


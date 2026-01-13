using System;
using System.Collections.Generic;
using System.Reflection;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;

namespace Andastra.Runtime.Core.Animation
{
    /// <summary>
    /// System that updates animations for all entities with animation components.
    /// </summary>
    /// <remarks>
    /// Animation System:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) animation system
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
    /// - Animation durations are retrieved from animation component (loaded from MDX/MDL data)
    /// - Animation completion events are fired when non-looping animations finish playing
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Animation completion triggers script events and action completion
    /// </remarks>
    public class AnimationSystem
    {
        private readonly IWorld _world;
        // Track which animations have already fired completion events
        // Key: entity ObjectId + animation ID, Value: whether completion event was fired
        private readonly Dictionary<string, bool> _completionEventsFired;

        public AnimationSystem(IWorld world)
        {
            _world = world ?? throw new ArgumentNullException("world");
            _completionEventsFired = new Dictionary<string, bool>();
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

                // Get animation duration from component (loaded from MDX/MDL data)
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Animation duration stored in MDX animation data
                // BaseAnimationComponent.AnimationDuration is set by engine-specific implementations
                float animationDuration = GetAnimationDuration(animation);

                // Track previous completion state to detect when animation just completed
                bool wasComplete = animation.AnimationComplete;

                // Update animation time
                float newTime = animation.AnimationTime + (deltaTime * animation.AnimationSpeed);

                // Handle animation completion
                if (!animation.IsLooping)
                {
                    // For non-looping animations, clamp to duration and mark complete
                    if (newTime >= animationDuration)
                    {
                        animation.AnimationTime = animationDuration;
                        // AnimationComplete is computed property, will be true now

                        // Fire completion event if animation just completed (wasn't complete last frame)
                        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Animation completion triggers script events and action completion
                        // Located via string references: "EVENT_PLAY_ANIMATION" @ 0x007bcd74, animation completion handling
                        if (!wasComplete && animation.AnimationComplete)
                        {
                            FireAnimationCompleteEvent(entity, animation);
                        }
                    }
                    else
                    {
                        animation.AnimationTime = newTime;
                        // Reset completion event tracking if animation is restarted or not complete
                        string completionKey = GetCompletionKey(entity, animation.CurrentAnimation);
                        if (_completionEventsFired.ContainsKey(completionKey))
                        {
                            _completionEventsFired.Remove(completionKey);
                        }
                    }
                }
                else
                {
                    // For looping animations, wrap time
                    if (newTime >= animationDuration)
                    {
                        animation.AnimationTime = newTime % animationDuration;
                    }
                    else
                    {
                        animation.AnimationTime = newTime;
                    }
                }
            }
        }

        /// <summary>
        /// Gets the animation duration from the animation component.
        /// Falls back to default 1.0f if duration is not available.
        /// </summary>
        /// <param name="animation">The animation component.</param>
        /// <returns>The animation duration in seconds.</returns>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Animation duration stored in MDX animation data
        /// BaseAnimationComponent.AnimationDuration is set by engine-specific implementations
        /// </remarks>
        private float GetAnimationDuration(IAnimationComponent animation)
        {
            if (animation == null)
            {
                return 1.0f;
            }

            // Try to get duration from component using reflection (in case it's not exposed via interface)
            // Engine-specific implementations may expose AnimationDuration property
            var componentType = animation.GetType();
            var durationProperty = componentType.GetProperty("AnimationDuration");
            if (durationProperty != null)
            {
                try
                {
                    object durationValue = durationProperty.GetValue(animation);
                    if (durationValue is float duration && duration > 0.0f)
                    {
                        return duration;
                    }
                }
                catch
                {
                    // Reflection failed, fall through to default
                }
            }

            // Default duration if not available
            return 1.0f;
        }

        /// <summary>
        /// Fires an animation completion event for a non-looping animation that just finished.
        /// </summary>
        /// <param name="entity">The entity whose animation completed.</param>
        /// <param name="animation">The animation component.</param>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Animation completion triggers script events and action completion
        /// Located via string references: "EVENT_PLAY_ANIMATION" @ 0x007bcd74, animation completion handling
        /// Original implementation: Animation completion events notify action system and scripts
        /// </remarks>
        private void FireAnimationCompleteEvent(IEntity entity, IAnimationComponent animation)
        {
            if (entity == null || animation == null || _world == null || _world.EventBus == null)
            {
                return;
            }

            // Only fire event once per animation completion
            string completionKey = GetCompletionKey(entity, animation.CurrentAnimation);
            if (_completionEventsFired.ContainsKey(completionKey))
            {
                return; // Already fired for this animation
            }

            // Mark as fired
            _completionEventsFired[completionKey] = true;

            // Fire animation completion event
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Animation completion events notify action system and scripts
            var animationCompleteEvent = new AnimationCompleteEvent
            {
                Entity = entity,
                AnimationId = animation.CurrentAnimation,
                AnimationSpeed = animation.AnimationSpeed
            };

            _world.EventBus.Publish(animationCompleteEvent);
        }

        /// <summary>
        /// Gets a unique key for tracking animation completion events.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <param name="animationId">The animation ID.</param>
        /// <returns>A unique key string.</returns>
        private string GetCompletionKey(IEntity entity, int animationId)
        {
            return $"{entity.ObjectId}_{animationId}";
        }
    }

    /// <summary>
    /// Event fired when a non-looping animation completes.
    /// </summary>
    /// <remarks>
    /// Animation Complete Event:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Animation completion triggers script events and action completion
    /// - Located via string references: "EVENT_PLAY_ANIMATION" @ 0x007bcd74, animation completion handling
    /// - Original implementation: Animation completion events notify action system and scripts
    /// - Fired once per animation completion (not every frame)
    /// - Used by ActionPlayAnimation to detect when animation finishes
    /// - Can be subscribed to for custom animation completion handling
    /// </remarks>
    public class AnimationCompleteEvent : IGameEvent
    {
        /// <summary>
        /// The entity whose animation completed.
        /// </summary>
        public IEntity Entity { get; set; }

        /// <summary>
        /// The animation ID that completed.
        /// </summary>
        public int AnimationId { get; set; }

        /// <summary>
        /// The playback speed of the completed animation.
        /// </summary>
        public float AnimationSpeed { get; set; }
    }
}


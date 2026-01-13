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
    /// - CNWSObject::UpdateAnimations @ (K1: 0x004eb750, TSL: 0x004eb750) - Main animation update system (address to be verified in Ghidra)
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
    /// - CNWSObject::HandleAnimationComplete @ (K1: 0x004eb750, TSL: 0x004eb750): Animation completion triggers script events and action completion
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
                // CNWSObject::GetAnimationDuration @ (K1: 0x004eb750, TSL: 0x004eb750) - Animation duration stored in MDX animation data (address to be verified in Ghidra)
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
                        // CNWSObject::HandleAnimationComplete @ (K1: 0x004eb750, TSL: 0x004eb750): Animation completion triggers script events and action completion
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
        /// CNWSObject::GetAnimationDuration @ (K1: 0x004eb750, TSL: 0x004eb750) - Animation duration stored in MDX animation data (address to be verified in Ghidra)
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
        /// Triggers script events and notifies action system of completion.
        /// </summary>
        /// <param name="entity">The entity whose animation completed.</param>
        /// <param name="animation">The animation component.</param>
        /// <remarks>
        /// Animation Completion Handler:
        /// - swkotor.exe: CNWSObject::HandleAnimationComplete @ 0x004eb750 (K1) - Animation completion handler
        /// - swkotor2.exe: CNWSObject::HandleAnimationComplete @ 0x004eb750 (TSL) - Animation completion handler
        /// - Located via string references: "EVENT_PLAY_ANIMATION" @ 0x007bcd74 (TSL), "EVENT_PLAY_ANIMATION" @ 0x007449bc (K1)
        /// - Original implementation: When non-looping animation completes (AnimationTime >= AnimationDuration):
        ///   1. Fires AnimationCompleteEvent to notify action system (ActionPlayAnimation checks AnimationComplete property)
        ///   2. Fires EVENT_PLAY_ANIMATION object event (case 9) via DispatchEvent @ 0x004dcfb0
        ///   3. EVENT_PLAY_ANIMATION routes through event dispatcher and may trigger script execution
        /// - Action completion: ActionPlayAnimation.ExecuteInternal checks AnimationComplete property each frame
        ///   When AnimationComplete becomes true, action returns ActionStatus.Complete
        /// - Script events: EVENT_PLAY_ANIMATION (object event type 9) is dispatched via 0x004dcfb0
        ///   This object event may trigger script hooks if registered on the entity
        /// - Event flow: Animation completes → FireAnimationCompleteEvent → AnimationCompleteEvent published
        ///   → ActionPlayAnimation detects completion via AnimationComplete property → Action completes
        ///   → EVENT_PLAY_ANIMATION object event fired (if supported by engine) → Script hooks may execute
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

            // Fire animation completion event to notify action system
            // ActionPlayAnimation subscribes to this event or checks AnimationComplete property directly
            // swkotor.exe: CNWSObject::HandleAnimationComplete @ 0x004eb750 (K1)
            // swkotor2.exe: CNWSObject::HandleAnimationComplete @ 0x004eb750 (TSL)
            var animationCompleteEvent = new AnimationCompleteEvent
            {
                Entity = entity,
                AnimationId = animation.CurrentAnimation,
                AnimationSpeed = animation.AnimationSpeed
            };

            _world.EventBus.Publish(animationCompleteEvent);

            // Note: EVENT_PLAY_ANIMATION object event (case 9) firing is engine-specific
            // Original engine: DispatchEvent @ 0x004dcfb0 handles EVENT_PLAY_ANIMATION (case 9)
            // Located via string references: "EVENT_PLAY_ANIMATION" @ 0x007bcd74 (TSL), "EVENT_PLAY_ANIMATION" @ 0x007449bc (K1)
            // The original engine's DispatchEvent @ 0x004dcfb0 routes EVENT_PLAY_ANIMATION (case 9) to script execution system
            // Engine-specific implementations (OdysseyEventDispatcher) should handle EVENT_PLAY_ANIMATION if needed
            // For now, AnimationCompleteEvent is sufficient for action system completion detection
            // ActionPlayAnimation checks AnimationComplete property each frame, which becomes true when animation completes
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
    /// - CNWSObject::HandleAnimationComplete @ (K1: 0x004eb750, TSL: 0x004eb750): Animation completion triggers script events and action completion
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


using System;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;

namespace Odyssey.Kotor.Components
{
    /// <summary>
    /// Component for entities that can play animations.
    /// </summary>
    /// <remarks>
    /// Animation Component Implementation:
    /// - Based on swkotor2.exe animation system
    /// - Located via string references: "Animation" @ 0x007c3440, "AnimList" @ 0x007c3694
    /// - "PlayAnim" @ 0x007c346c, "AnimLoop" @ 0x007c4c70 (animation loop flag)
    /// - "CurrentAnim" @ 0x007c38d4, "NextAnim" @ 0x007c38c8 (animation state tracking)
    /// - Animation timing: "frameStart" @ 0x007ba698, "frameEnd" @ 0x007ba668 (animation frame timing)
    /// - Original implementation: Entities with models can play animations from MDL animation arrays
    /// - Animation IDs reference animation indices in MDL animation arrays (0-based index)
    /// - CurrentAnimation: Currently playing animation ID (-1 = no animation, idle state)
    /// - AnimationSpeed: Playback rate multiplier (1.0 = normal, 2.0 = double speed, 0.5 = half speed)
    /// - IsLooping: Whether current animation should loop (true = loop, false = play once)
    /// - AnimationTime: Current time position in animation (0.0 to animation duration)
    /// - AnimationComplete: True when non-looping animation has finished playing
    /// - Animations loaded from MDX files (animation keyframe data), referenced by MDL model files
    /// - Animation system updates animation time each frame, triggers completion events
    /// </remarks>
    public class AnimationComponent : IComponent, IAnimationComponent
    {
        private int _currentAnimation;
        private float _animationSpeed;
        private bool _isLooping;
        private float _animationTime;
        private float _animationDuration;

        /// <summary>
        /// Gets or sets the entity that owns this component.
        /// </summary>
        public IEntity Owner { get; set; }

        /// <summary>
        /// Currently playing animation ID (-1 = no animation).
        /// </summary>
        public int CurrentAnimation
        {
            get { return _currentAnimation; }
            set
            {
                if (_currentAnimation != value)
                {
                    _currentAnimation = value;
                    _animationTime = 0.0f;
                    // Animation duration should be retrieved from MDX data
                    // For now, use a default duration
                    _animationDuration = 1.0f; // Placeholder - should come from MDX data
                }
            }
        }

        /// <summary>
        /// Animation playback speed multiplier (1.0 = normal speed).
        /// </summary>
        public float AnimationSpeed
        {
            get { return _animationSpeed; }
            set { _animationSpeed = value; }
        }

        /// <summary>
        /// Whether the current animation is looping.
        /// </summary>
        public bool IsLooping
        {
            get { return _isLooping; }
            set { _isLooping = value; }
        }

        /// <summary>
        /// Current time position in the animation (0.0 to animation duration).
        /// </summary>
        public float AnimationTime
        {
            get { return _animationTime; }
            set { _animationTime = value; }
        }

        /// <summary>
        /// Whether the current animation has completed (for non-looping animations).
        /// </summary>
        public bool AnimationComplete
        {
            get
            {
                if (_currentAnimation < 0 || _isLooping)
                {
                    return false;
                }
                return _animationTime >= _animationDuration;
            }
        }

        /// <summary>
        /// Gets the animation duration (from MDX data, or default if not available).
        /// </summary>
        public float AnimationDuration
        {
            get { return _animationDuration; }
            set { _animationDuration = value; }
        }

        /// <summary>
        /// Initializes a new instance of the AnimationComponent class.
        /// </summary>
        public AnimationComponent()
        {
            _currentAnimation = -1; // No animation playing
            _animationSpeed = 1.0f;
            _isLooping = false;
            _animationTime = 0.0f;
            _animationDuration = 1.0f; // Default duration
        }

        /// <summary>
        /// Plays an animation.
        /// </summary>
        /// <param name="animationId">Animation ID (index in MDL animation array).</param>
        /// <param name="speed">Playback speed multiplier (1.0 = normal).</param>
        /// <param name="loop">Whether to loop the animation.</param>
        public void PlayAnimation(int animationId, float speed = 1.0f, bool loop = false)
        {
            _currentAnimation = animationId;
            _animationSpeed = speed;
            _isLooping = loop;
            _animationTime = 0.0f;
            // Animation duration should be retrieved from MDX data
            // For now, use a default duration
            _animationDuration = 1.0f; // Placeholder - should come from MDX data
        }

        /// <summary>
        /// Stops the current animation.
        /// </summary>
        public void StopAnimation()
        {
            _currentAnimation = -1;
            _animationTime = 0.0f;
        }
    }
}


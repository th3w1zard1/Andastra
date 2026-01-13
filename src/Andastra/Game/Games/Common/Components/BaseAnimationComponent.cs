using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Common.Components
{
    /// <summary>
    /// Base implementation of animation component functionality shared across all BioWare engines.
    /// </summary>
    /// <remarks>
    /// Base Animation Component Implementation:
    /// - Common animation properties and methods across all engines
    /// - Handles animation state, playback speed, looping, timing, and completion
    /// - Provides base for engine-specific animation component implementations
    /// - Cross-engine analysis: All engines share common animation structure patterns
    /// - Common functionality: CurrentAnimation, AnimationSpeed, IsLooping, AnimationTime, AnimationComplete, PlayAnimation, StopAnimation
    /// - Engine-specific: Animation file format details, animation system integration, event handling, duration calculation
    ///
    /// Based on reverse engineering analysis of:
    /// - swkotor.exe: Animation system with MDL/MDX animation arrays
    /// - swkotor2.exe: Enhanced animation system (FUN_005223a0 @ 0x005223a0 loads animation data, FUN_00589520 @ 0x00589520 handles animation state)
    /// - nwmain.exe: Aurora animation system using Gob::PlayAnimation (0x140052580) with Animation class hierarchy
    /// - daorigins.exe: Eclipse animation system with animation tree support
    /// - DragonAge2.exe: Enhanced Eclipse animation system
    /// - /: Infinity animation system with animation state tracking
    ///
    /// Common structure across engines:
    /// - CurrentAnimation (int): Currently playing animation ID (-1 = no animation, idle state)
    /// - AnimationSpeed (float): Playback rate multiplier (1.0 = normal speed, 2.0 = double speed, 0.5 = half speed)
    /// - IsLooping (bool): Whether current animation should loop (true = loop, false = play once)
    /// - AnimationTime (float): Current time position in animation (0.0 to animation duration)
    /// - AnimationComplete (bool): True when non-looping animation has finished playing
    /// - AnimationDuration (float): Duration of current animation (from animation data)
    ///
    /// Common animation operations across engines:
    /// - PlayAnimation: Starts playing an animation with optional speed and loop parameters
    /// - StopAnimation: Stops the current animation and resets to idle state
    /// - Animation completion: Detected when AnimationTime >= AnimationDuration for non-looping animations
    /// </remarks>
    [PublicAPI]
    public abstract class BaseAnimationComponent : IAnimationComponent
    {
        protected int _currentAnimation;
        protected float _animationSpeed;
        protected bool _isLooping;
        protected float _animationTime;
        protected float _animationDuration;

        /// <summary>
        /// The entity this component is attached to.
        /// </summary>
        public IEntity Owner { get; set; }

        /// <summary>
        /// Currently playing animation ID (-1 = no animation).
        /// </summary>
        public virtual int CurrentAnimation
        {
            get { return _currentAnimation; }
            set
            {
                if (_currentAnimation != value)
                {
                    _currentAnimation = value;
                    _animationTime = 0.0f;
                    // Animation duration should be retrieved from animation data
                    // Engine-specific implementations should override to load duration from MDX/MDL/other formats
                    _animationDuration = GetAnimationDuration(value);
                }
            }
        }

        /// <summary>
        /// Animation playback speed multiplier (1.0 = normal speed).
        /// </summary>
        public virtual float AnimationSpeed
        {
            get { return _animationSpeed; }
            set { _animationSpeed = value; }
        }

        /// <summary>
        /// Whether the current animation is looping.
        /// </summary>
        public virtual bool IsLooping
        {
            get { return _isLooping; }
            set { _isLooping = value; }
        }

        /// <summary>
        /// Current time position in the animation (0.0 to animation duration).
        /// </summary>
        public virtual float AnimationTime
        {
            get { return _animationTime; }
            set { _animationTime = value; }
        }

        /// <summary>
        /// Whether the current animation has completed (for non-looping animations).
        /// </summary>
        public virtual bool AnimationComplete
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
        /// Gets the animation duration (from animation data, or default if not available).
        /// </summary>
        public virtual float AnimationDuration
        {
            get { return _animationDuration; }
            set { _animationDuration = value; }
        }

        /// <summary>
        /// Initializes a new instance of the base animation component.
        /// </summary>
        protected BaseAnimationComponent()
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
        /// <param name="animationId">Animation ID (index in animation array or animation name ID).</param>
        /// <param name="speed">Playback speed multiplier (1.0 = normal).</param>
        /// <param name="loop">Whether to loop the animation.</param>
        public virtual void PlayAnimation(int animationId, float speed = 1.0f, bool loop = false)
        {
            _currentAnimation = animationId;
            _animationSpeed = speed;
            _isLooping = loop;
            _animationTime = 0.0f;
            // Animation duration should be retrieved from animation data
            // Engine-specific implementations should override to load duration from MDX/MDL/other formats
            _animationDuration = GetAnimationDuration(animationId);
        }

        /// <summary>
        /// Stops the current animation.
        /// </summary>
        public virtual void StopAnimation()
        {
            _currentAnimation = -1;
            _animationTime = 0.0f;
        }

        /// <summary>
        /// Gets the duration of an animation by ID.
        /// </summary>
        /// <param name="animationId">Animation ID to get duration for.</param>
        /// <returns>Animation duration in seconds, or default 1.0f if not available.</returns>
        /// <remarks>
        /// Engine-specific implementations should override this to load duration from animation data files.
        /// </remarks>
        protected virtual float GetAnimationDuration(int animationId)
        {
            // Default implementation returns 1.0f
            // Engine-specific implementations should override to load from MDX/MDL/other formats
            return 1.0f;
        }

        /// <summary>
        /// Called when the component is attached to an entity.
        /// </summary>
        public virtual void OnAttach()
        {
            // Base implementation does nothing - engine-specific implementations can override
        }

        /// <summary>
        /// Called when the component is detached from an entity.
        /// </summary>
        public virtual void OnDetach()
        {
            // Stop animation when component is detached
            StopAnimation();
        }
    }
}

namespace Andastra.Runtime.Core.Interfaces.Components
{
    /// <summary>
    /// Component for entities that can play animations.
    /// </summary>
    /// <remarks>
    /// Animation Component Interface:
    /// - TODO: lookup data from daorigins.exe/dragonage2.exe/masseffect.exe/masseffect2.exe/swkotor.exe/swkotor2.exe and split into subclass'd inheritence structures appropriately. parent class(es) should contain common code.
    /// - TODO: this should NOT specify swkotor2.exe unless it specifies the other exes as well!!!
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
    public interface IAnimationComponent : IComponent
    {
        /// <summary>
        /// Currently playing animation ID (-1 = no animation).
        /// </summary>
        int CurrentAnimation { get; set; }

        /// <summary>
        /// Animation playback speed multiplier (1.0 = normal speed).
        /// </summary>
        float AnimationSpeed { get; set; }

        /// <summary>
        /// Whether the current animation is looping.
        /// </summary>
        bool IsLooping { get; set; }

        /// <summary>
        /// Current time position in the animation (0.0 to animation duration).
        /// </summary>
        float AnimationTime { get; set; }

        /// <summary>
        /// Whether the current animation has completed (for non-looping animations).
        /// </summary>
        bool AnimationComplete { get; }

        /// <summary>
        /// Plays an animation.
        /// </summary>
        /// <param name="animationId">Animation ID (index in MDL animation array).</param>
        /// <param name="speed">Playback speed multiplier (1.0 = normal).</param>
        /// <param name="loop">Whether to loop the animation.</param>
        void PlayAnimation(int animationId, float speed = 1.0f, bool loop = false);

        /// <summary>
        /// Stops the current animation.
        /// </summary>
        void StopAnimation();
    }
}


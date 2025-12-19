namespace Andastra.Runtime.Core.Interfaces.Components
{
    /// <summary>
    /// Component for entities that can play animations.
    /// </summary>
    /// <remarks>
    /// Animation Component Interface:
    /// - Common animation interface shared across all BioWare engines (Odyssey, Aurora, Eclipse, Infinity)
    /// - Base implementation: BaseAnimationComponent (Runtime.Games.Common.Components)
    /// - Engine-specific implementations:
    ///   - Odyssey: OdysseyAnimationComponent (swkotor.exe, swkotor2.exe)
    ///   - Aurora: AuroraAnimationComponent (nwmain.exe)
    ///   - Eclipse: EclipseAnimationComponent (daorigins.exe, DragonAge2.exe)
    ///   - Infinity: InfinityAnimationComponent (MassEffect.exe, MassEffect2.exe)
    /// - Common functionality: Animation playback, timing, looping, completion tracking
    /// - Animation IDs reference animation indices in model animation arrays (0-based index)
    /// - CurrentAnimation: Currently playing animation ID (-1 = no animation, idle state)
    /// - AnimationSpeed: Playback rate multiplier (1.0 = normal, 2.0 = double speed, 0.5 = half speed)
    /// - IsLooping: Whether current animation should loop (true = loop, false = play once)
    /// - AnimationTime: Current time position in animation (0.0 to animation duration)
    /// - AnimationComplete: True when non-looping animation has finished playing
    /// - Animations loaded from engine-specific formats (MDX/MDL for Odyssey/Aurora, animation trees for Eclipse/Infinity)
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
        /// <param name="animationId">Animation ID (engine-specific: index, name hash, node ID, etc.).</param>
        /// <param name="speed">Playback speed multiplier (1.0 = normal).</param>
        /// <param name="loop">Whether to loop the animation.</param>
        void PlayAnimation(int animationId, float speed = 1.0f, bool loop = false);

        /// <summary>
        /// Stops the current animation.
        /// </summary>
        void StopAnimation();
    }
}


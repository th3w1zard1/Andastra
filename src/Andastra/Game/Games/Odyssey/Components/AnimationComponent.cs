using System;
using Andastra.Runtime.Content.MDL;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Game.Games.Common.Components;

namespace Andastra.Game.Engines.Odyssey.Components
{
    /// <summary>
    /// Odyssey engine-specific implementation of animation component.
    /// </summary>
    /// <remarks>
    /// Odyssey Animation Component Implementation:
    /// - Inherits from BaseAnimationComponent (Runtime.Games.Common.Components)
    /// - Based on swkotor.exe and swkotor2.exe animation systems
    /// - Located via string references: "Animation" @ 0x007bf604 (swkotor2.exe), "Animation" @ 0x00746060 (swkotor.exe)
    /// - "AnimationTime" @ 0x007bf810 (swkotor2.exe), "AnimationLength" @ 0x007bf980 (swkotor2.exe)
    /// - "AnimationState" @ 0x007c1f30 (swkotor2.exe)
    /// - Original implementation: Entities with models can play animations from MDL animation arrays
    /// - Animation IDs reference animation indices in MDL animation arrays (0-based index)
    /// - Animations loaded from MDX files (animation keyframe data), referenced by MDL model files
    /// - MDX format: Contains animation keyframe data with timing information
    /// - MDL format: References MDX files and contains animation array indices
    /// - Animation system updates animation time each frame, triggers completion events
    /// - Animation duration loaded from MDX animation header at offset 0x50 (80 bytes) as float
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): AnimationLength field accessed from MDL animation data structure
    /// </remarks>
    public class OdysseyAnimationComponent : BaseAnimationComponent
    {
        /// <summary>
        /// Initializes a new instance of the OdysseyAnimationComponent class.
        /// </summary>
        public OdysseyAnimationComponent()
            : base()
        {
            // Base class initializes all fields
        }

        /// <summary>
        /// Gets the duration of an animation by ID from MDX data.
        /// </summary>
        /// <param name="animationId">Animation ID to get duration for (0-based index into animation array).</param>
        /// <returns>Animation duration in seconds from MDX data, or 1.0 if not available.</returns>
        /// <remarks>
        /// Animation Duration Loading:
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): AnimationLength field accessed from MDL animation data
        /// - Located via string references: "AnimationLength" @ 0x007bf980 (swkotor2.exe)
        /// - MDX format: Animation header contains duration at offset 0x50 (80 bytes) as float
        /// - MDL format: MDLAnimationData.Length field contains animation duration in seconds
        /// - Animation ID is 0-based index into MDL model's Animations array
        /// - Model loaded from MDLCache using entity's ModelResRef from RenderableComponent
        /// - If model not cached or animation ID invalid, returns default duration (1.0 seconds)
        /// </remarks>
        protected override float GetAnimationDuration(int animationId)
        {
            // Validate animation ID
            if (animationId < 0)
            {
                return 1.0f;
            }

            // Get entity's renderable component to access model ResRef
            if (Owner == null)
            {
                return 1.0f;
            }

            IRenderableComponent renderable = Owner.GetComponent<IRenderableComponent>();
            if (renderable == null || string.IsNullOrEmpty(renderable.ModelResRef))
            {
                return 1.0f;
            }

            // Try to get model from cache
            MDLModel model;
            if (!MDLCache.Instance.TryGet(renderable.ModelResRef, out model))
            {
                // Model not in cache - return default duration
                // Models should typically be loaded before animations are played
                return 1.0f;
            }

            // Validate model has animations
            if (model.Animations == null || model.Animations.Length == 0)
            {
                return 1.0f;
            }

            // Validate animation ID is within bounds
            if (animationId >= model.Animations.Length)
            {
                return 1.0f;
            }

            // Get animation duration from MDX data
            MDLAnimationData animation = model.Animations[animationId];
            if (animation == null)
            {
                return 1.0f;
            }

            // Return animation duration (loaded from MDX animation header at offset 0x50)
            // MDX format: Animation header contains duration as float at offset 80 (0x50) bytes
            // This value is already parsed and stored in MDLAnimationData.Length during MDL/MDX loading
            float duration = animation.Length;

            // Validate duration is positive (should always be, but check for safety)
            if (duration <= 0.0f)
            {
                return 1.0f;
            }

            return duration;
        }
    }
}


using System;
using System.Collections.Generic;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Game.Games.Common.Components;

namespace Andastra.Game.Games.Eclipse.Components
{
    /// <summary>
    /// Eclipse engine-specific implementation of animation component functionality.
    /// </summary>
    /// <remarks>
    /// Eclipse Animation Component Implementation:
    /// - Inherits from BaseAnimationComponent (Runtime.Games.Common.Components)
    /// - Based on daorigins.exe and DragonAge2.exe animation systems
    /// - Located via string references: "Animation" @ 0x00ae5e10 (daorigins.exe), "Animation" @ 0x00bdde14 (DragonAge2.exe)
    /// - "KAnimationNode" @ 0x00ae5e52 (daorigins.exe), "AnimationNode" @ 0x00bdde14 (DragonAge2.exe)
    /// - "AnimationTree" @ 0x00ae5e70 (daorigins.exe), "AnimationTree" @ 0x00bdde30 (DragonAge2.exe)
    /// - "ModelAnimationTree" @ 0x00ae5e8c (daorigins.exe), "ModelAnimationTree" @ 0x00bdde4c (DragonAge2.exe)
    /// - "AnimationTask" @ 0x00b53fa4 (daorigins.exe), "@AnimationTask" @ 0x00bddb7e (DragonAge2.exe)
    /// - "EnableAnimation" @ 0x00afb878 (daorigins.exe), "DEBUG_EnableAnimation" @ 0x00bfab94 (DragonAge2.exe)
    /// - "AnimationEventDispatch" @ 0x00bddbc0 (DragonAge2.exe)
    /// - Original implementation: Animation system uses animation trees and animation nodes
    /// - Animation IDs reference animations in animation trees or animation node hierarchies
    /// - Animations managed through animation tree system with hierarchical animation nodes
    /// - Animation system supports animation blending, animation state machines, animation tasks, facial animation
    /// - Eclipse-specific: Animation tree system, animation node hierarchy, animation state machines, facial animation support
    /// - Animation durations are loaded from MDL animation data (MDLAnimationData.Length field)
    /// - Animation durations are cached per model/animation ID combination for performance
    /// - Eclipse engine uses MDL format for model and animation data (same as Odyssey/Aurora engines)
    /// - MDL animations are part of the animation tree system - animation trees reference MDL animations
    /// </remarks>
    public class EclipseAnimationComponent : BaseAnimationComponent
    {
        /// <summary>
        /// Cache for animation durations keyed by model ResRef and animation ID.
        /// Format: "ModelResRef:AnimationID" -> duration in seconds
        /// </summary>
        private readonly Dictionary<string, float> _animationDurationCache;

        /// <summary>
        /// Initializes a new instance of the EclipseAnimationComponent class.
        /// </summary>
        public EclipseAnimationComponent()
            : base()
        {
            _animationDurationCache = new Dictionary<string, float>();
        }

        /// <summary>
        /// Gets the duration of an animation by ID from Eclipse animation tree data.
        /// </summary>
        /// <param name="animationId">Animation ID (index in MDL animation array or animation name hash).</param>
        /// <returns>Animation duration in seconds from animation tree data, or default 1.0f if not available.</returns>
        /// <remarks>
        /// Eclipse-specific: Loads animation duration from MDL animation data (part of animation tree system).
        /// Animation IDs reference animations in MDL animation arrays, which are part of the animation tree system.
        /// Based on daorigins.exe/DragonAge2.exe: Animation tree system with animation nodes
        /// Located via string references: "AnimationTree" @ 0x00ae5e70 (daorigins.exe), "AnimationTree" @ 0x00bdde30 (DragonAge2.exe)
        /// "ModelAnimationTree" @ 0x00ae5e8c (daorigins.exe), "ModelAnimationTree" @ 0x00bdde4c (DragonAge2.exe)
        /// "AnimationTask" @ 0x00b53fa4 (daorigins.exe), "@AnimationTask" @ 0x00bddb7e (DragonAge2.exe)
        /// Original implementation: Animation duration stored in MDLAnimationData.Length field (loaded from MDL/MDX files)
        /// Eclipse engine uses MDL format for model and animation data (same as Odyssey/Aurora engines)
        /// MDL animations are part of the animation tree system - animation trees reference MDL animations
        /// Animation durations are cached per model/animation ID combination for performance optimization
        /// Cache key format: "ModelResRef:AnimationID" -> duration in seconds
        /// Cache is cleared when component is detached to prevent memory leaks
        /// </remarks>
        protected override float GetAnimationDuration(int animationId)
        {
            // Validate animation ID
            if (animationId < 0)
            {
                return 1.0f; // Default duration for invalid animation ID
            }

            // Get entity's model resource reference for cache key
            if (Owner == null)
            {
                return 1.0f; // Default duration if no owner entity
            }

            IRenderableComponent renderable = Owner.GetComponent<IRenderableComponent>();
            if (renderable == null || string.IsNullOrEmpty(renderable.ModelResRef))
            {
                return 1.0f; // Default duration if no renderable component or model ResRef
            }

            // Check cache first for performance optimization
            // Cache key: "ModelResRef:AnimationID" format
            // Using string interpolation for better performance than string.Format
            string cacheKey = $"{renderable.ModelResRef}:{animationId}";
            if (_animationDurationCache.TryGetValue(cacheKey, out float cachedDuration))
            {
                return cachedDuration; // Return cached duration if available
            }

            // Load animation duration from MDL model data
            // Based on Eclipse engine: Animation duration loaded from MDL animation data structure
            // Eclipse uses MDL format similar to Odyssey/Aurora for model and animation data
            // MDLAnimationData.Length field contains animation duration in seconds
            // Models are cached in MDLCache for performance (shared across all components)

            // Try to get model from MDL cache
            Runtime.Content.MDL.MDLModel model;
            if (!Runtime.Content.MDL.MDLCache.Instance.TryGet(renderable.ModelResRef, out model))
            {
                // Model not in cache - return default duration
                // Models should typically be loaded before animations are played
                // Cache the default duration to avoid repeated lookups for missing models
                _animationDurationCache[cacheKey] = 1.0f;
                return 1.0f;
            }

            // Validate model has animations loaded
            if (model.Animations == null || model.Animations.Length == 0)
            {
                // No animations in model - cache default duration
                _animationDurationCache[cacheKey] = 1.0f;
                return 1.0f;
            }

            // Validate animation ID is within bounds
            if (animationId >= model.Animations.Length)
            {
                // Animation ID out of range - cache default duration
                _animationDurationCache[cacheKey] = 1.0f;
                return 1.0f;
            }

            // Get animation data from MDL animation array
            // Based on Eclipse engine: MDLAnimationData.Length contains animation duration in seconds
            Runtime.Content.MDL.MDLAnimationData animation = model.Animations[animationId];
            if (animation == null)
            {
                // Animation data is null - cache default duration
                _animationDurationCache[cacheKey] = 1.0f;
                return 1.0f;
            }

            // Get animation duration from MDL animation data
            // This value is already parsed and stored in MDLAnimationData.Length during MDL/MDX loading
            // MDX format: Animation header contains duration as float at offset 80 (0x50) bytes
            // MDL format: MDLAnimationData.Length field contains animation duration in seconds
            float duration = animation.Length;

            // Validate duration is positive (should always be, but check for safety)
            // If duration is invalid (<= 0), use default duration
            if (duration <= 0.0f)
            {
                _animationDurationCache[cacheKey] = 1.0f;
                return 1.0f;
            }

            // Cache the valid duration for future lookups
            _animationDurationCache[cacheKey] = duration;
            return duration;
        }

        /// <summary>
        /// Called when the component is attached to an entity.
        /// </summary>
        public override void OnAttach()
        {
            base.OnAttach();
            // Eclipse-specific initialization if needed
            // Animation duration cache is ready for use
        }

        /// <summary>
        /// Called when the component is detached from an entity.
        /// </summary>
        public override void OnDetach()
        {
            // Clear animation duration cache to prevent memory leaks
            // Cache is per-component, so clearing on detach ensures no stale references
            _animationDurationCache.Clear();
            base.OnDetach();
        }
    }
}


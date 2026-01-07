using Andastra.Runtime.Content.MDL;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Games.Common.Components;

namespace Andastra.Runtime.Games.Aurora.Components
{
    /// <summary>
    /// Aurora engine-specific implementation of animation component functionality.
    /// </summary>
    /// <remarks>
    /// Aurora Animation Component Implementation:
    /// - Inherits from BaseAnimationComponent (Runtime.Games.Common.Components)
    /// - Based on nwmain.exe animation system
    /// - Located via string references: "Animation" @ 0x140ddc0e0 (nwmain.exe)
    /// - "AnimationTime" @ 0x140ddc0f0 (nwmain.exe), "AnimationLength" @ 0x140ddc218 (nwmain.exe)
    /// - "AnimationSpeed" @ 0x140df1258 (nwmain.exe), "AnimationState" @ 0x140de75d0 (nwmain.exe)
    /// - "AnimationReplace" @ 0x140df1420 (nwmain.exe), "AnimationReplaceList" @ 0x140df1438 (nwmain.exe)
    /// - Original implementation: Gob::PlayAnimation @ 0x140052580 handles animation playback
    /// - CNWSObject::AIActionPlayAnimation @ 0x1404a4700 handles AI-driven animation actions
    /// - CNWSVirtualMachineCommands::ExecuteCommandPlayAnimation @ 0x140538060 handles script command animation playback
    /// - Animation class hierarchy: Animation base class, CustomAnimation, LookAtAnimation subclasses
    /// - Animation IDs reference animations by name or index in animation arrays
    /// - Animations managed through Animation class hierarchy with CExoArrayList&lt;Animation*&gt;
    /// - Animation system supports queued animations, fire-and-forget animations, animation replacement
    /// - Aurora-specific: Animation name-based lookup, animation queuing system, animation replacement lists
    /// - Animation duration loaded from MDX animation header at offset 0x50 (80 bytes) as float
    /// - Based on nwmain.exe: AnimationLength field accessed from MDL animation data structure
    /// - Aurora uses MDL/MDX format (same as Odyssey) for model and animation data
    /// </remarks>
    public class AuroraAnimationComponent : BaseAnimationComponent
    {
        /// <summary>
        /// Initializes a new instance of the AuroraAnimationComponent class.
        /// </summary>
        public AuroraAnimationComponent()
            : base()
        {
            // Aurora-specific initialization if needed
        }

        /// <summary>
        /// Gets the duration of an animation by ID from Aurora animation data.
        /// </summary>
        /// <param name="animationId">Animation ID (0-based index into animation array).</param>
        /// <returns>Animation duration in seconds from animation data, or default 1.0f if not available.</returns>
        /// <remarks>
        /// Animation Duration Loading:
        /// - Based on nwmain.exe: AnimationLength field accessed from MDL animation data
        /// - Located via string references: "AnimationLength" @ 0x140ddc218 (nwmain.exe)
        /// - MDX format: Animation header contains duration at offset 0x50 (80 bytes) as float
        /// - MDL format: MDLAnimationData.Length field contains animation duration in seconds
        /// - Animation ID is 0-based index into MDL model's Animations array
        /// - Model loaded from MDLCache using entity's ModelResRef from RenderableComponent
        /// - If model not cached or animation ID invalid, returns default duration (1.0 seconds)
        /// - Aurora uses the same MDL/MDX format as Odyssey, so implementation follows same pattern
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

        /// <summary>
        /// Called when the component is attached to an entity.
        /// </summary>
        public override void OnAttach()
        {
            base.OnAttach();
            // Aurora-specific initialization if needed
        }
    }
}


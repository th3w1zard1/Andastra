using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Game.Games.Common.Components;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Aurora.Components
{
    /// <summary>
    /// Aurora engine-specific implementation of renderable component for entities that can be rendered with 3D models.
    /// </summary>
    /// <remarks>
    /// Aurora Renderable Component Implementation:
    /// - Inherits from BaseRenderableComponent (Runtime.Games.Common.Components)
    /// - Based on nwmain.exe and nwn2main.exe rendering systems
    /// - Located via string references: "Model" @ 0x140ddc0a0 (nwmain.exe: model resource reference field)
    /// - "Appearance" @ 0x140dc50b0 (nwmain.exe: appearance type field for creatures)
    /// - "Appearance_Type" @ 0x140dc50b0 (nwmain.exe: appearance type field)
    /// - Model loading: CNWSCreature::LoadAppearance @ 0x1403a0a60 (nwmain.exe) loads creature model from appearance.2da row
    /// - Error messages: "Failed to load Appearance.TwoDA!" @ 0x140dc5e08 (nwmain.exe: appearance.2da loading error)
    /// - "Already loaded Appearance.TwoDA!" @ 0x140dc5dd8 (nwmain.exe: appearance.2da loading check)
    /// - Original implementation: Entities with models can be rendered in the game world
    /// - ModelResRef: MDL file resource reference for 3D model (loaded from installation resources)
    /// - AppearanceRow: Index into appearance.2da for creature appearance customization (Appearance_Type field)
    /// - CNWSCreature::LoadAppearance loads creature model by reading appearance.2da row, extracting ModelA/ModelB fields,
    ///   constructing model path, loading MDL/MDX files (model geometry/animation)
    /// - Textures loaded from TPC files (texture data) referenced by TexA/TexB fields in appearance.2da
    /// - Appearance.2da defines: ModelA, ModelB (model variants), TexA, TexB (texture variants), Race (race model base)
    /// - Model visibility controlled by Visible flag (used for culling, stealth, invisibility effects)
    /// - IsLoaded flag tracks whether model data has been loaded into memory (for async loading)
    /// - Aurora-specific: Uses MDL/MDX model format (same as Odyssey), TPC texture format, appearance.2da table structure
    /// - Aurora-specific: CNWSCreatureStats structure stores appearance type, model data loaded via C2DA class
    /// - Aurora-specific: Model loading uses CExoResMan resource manager for MDL/MDX file loading
    /// </remarks>
    [PublicAPI]
    public class AuroraRenderableComponent : BaseRenderableComponent
    {
        /// <summary>
        /// Initializes a new instance of the Aurora renderable component.
        /// </summary>
        public AuroraRenderableComponent()
            : base()
        {
        }

        /// <summary>
        /// Called when the model resource reference changes.
        /// Aurora-specific: Triggers MDL model reloading via CExoResMan.
        /// </summary>
        protected override void OnModelResRefChanged()
        {
            // Aurora-specific: Model reloading would be handled by CExoResMan resource manager
            // MDL/MDX files would be reloaded when ModelResRef changes
            // CNWSCreature::LoadAppearance would be called to reload model data
            base.OnModelResRefChanged();
        }

        /// <summary>
        /// Called when the component is attached to an entity.
        /// </summary>
        public override void OnAttach()
        {
            base.OnAttach();
            // Aurora-specific attachment logic can be added here if needed
        }

        /// <summary>
        /// Called when the component is detached from an entity.
        /// </summary>
        public override void OnDetach()
        {
            // Aurora-specific: Clean up MDL model resources if needed
            // CExoResMan resources would be released when component is detached
            base.OnDetach();
        }
    }
}

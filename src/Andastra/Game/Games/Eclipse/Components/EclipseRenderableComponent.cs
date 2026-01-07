using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Games.Common.Components;
using JetBrains.Annotations;

namespace Andastra.Runtime.Games.Eclipse.Components
{
    /// <summary>
    /// Eclipse engine-specific implementation of renderable component for entities that can be rendered with 3D models.
    /// </summary>
    /// <remarks>
    /// Eclipse Renderable Component Implementation:
    /// - Inherits from BaseRenderableComponent (Runtime.Games.Common.Components)
    /// - Based on daorigins.exe and DragonAge2.exe rendering systems
    /// - Located via string references: "Model" @ 0x00af4f68 (daorigins.exe: model resource reference field)
    /// - "Appearance_Type" @ 0x00bf0b9c (DragonAge2.exe: appearance type field for creatures)
    /// - "Appearance" @ 0x00b13674 (daorigins.exe: appearance type field)
    /// - Model loading: BioWare::Creature::LoadModel (daorigins.exe) loads creature model from appearance.2da row
    /// - Error messages: Model loading error messages in Eclipse engine executables
    /// - Original implementation: Entities with models can be rendered in the game world
    /// - ModelResRef: Engine-specific model format resource reference (GR2/MMH for Eclipse, loaded from installation resources)
    /// - AppearanceRow: Index into appearance.2da for creature appearance customization (Appearance_Type field)
    /// - Eclipse model loading loads creature model by reading appearance.2da row, extracting model fields,
    ///   constructing model path, loading GR2/MMH files (model geometry/animation)
    /// - Textures loaded from DDS/TEX files (texture data) referenced by texture fields in appearance.2da
    /// - Appearance.2da defines: Model variants, texture variants, race model base (structure similar to Odyssey/Aurora)
    /// - Model visibility controlled by Visible flag (used for culling, stealth, invisibility effects)
    /// - IsLoaded flag tracks whether model data has been loaded into memory (for async loading)
    /// - Eclipse-specific: Uses GR2/MMH model format (different from Odyssey/Aurora MDL/MDX format)
    /// - Eclipse-specific: Uses DDS/TEX texture format (different from Odyssey/Aurora TPC format)
    /// - Eclipse-specific: Appearance.2da table structure may have different column names than Odyssey/Aurora
    /// - Eclipse-specific: Model loading uses different resource manager system than Odyssey/Aurora
    /// - Eclipse-specific: PhysX integration for collision/physics may affect model loading and rendering
    /// </remarks>
    [PublicAPI]
    public class EclipseRenderableComponent : BaseRenderableComponent
    {
        /// <summary>
        /// Initializes a new instance of the Eclipse renderable component.
        /// </summary>
        public EclipseRenderableComponent()
            : base()
        {
        }

        /// <summary>
        /// Called when the model resource reference changes.
        /// Eclipse-specific: Triggers GR2/MMH model reloading.
        /// </summary>
        protected override void OnModelResRefChanged()
        {
            // Eclipse-specific: Model reloading would be handled by Eclipse resource manager
            // GR2/MMH files would be reloaded when ModelResRef changes
            // BioWare::Creature::LoadModel would be called to reload model data
            base.OnModelResRefChanged();
        }

        /// <summary>
        /// Called when the component is attached to an entity.
        /// </summary>
        public override void OnAttach()
        {
            base.OnAttach();
            // Eclipse-specific attachment logic can be added here if needed
            // May need to integrate with PhysX collision system for model-based collision
        }

        /// <summary>
        /// Called when the component is detached from an entity.
        /// </summary>
        public override void OnDetach()
        {
            // Eclipse-specific: Clean up GR2/MMH model resources if needed
            // Eclipse resource manager resources would be released when component is detached
            // PhysX collision shapes may need to be cleaned up if model-based collision is used
            base.OnDetach();
        }
    }
}

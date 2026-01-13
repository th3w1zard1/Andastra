using System;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Game.Games.Common.Components;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Odyssey.Components
{
    /// <summary>
    /// Odyssey engine-specific implementation of renderable component for entities that can be rendered with 3D models.
    /// </summary>
    /// <remarks>
    /// Odyssey Renderable Component Implementation:
    /// - Inherits from BaseRenderableComponent (Runtime.Games.Common.Components)
    /// - Based on swkotor.exe and swkotor2.exe rendering systems
    /// - Located via string references: "ModelResRef" @ 0x007c2f6c (swkotor2.exe: model resource reference field)
    /// - "Appearance_Type" @ 0x007c40f0 (swkotor2.exe: appearance type field for creatures)
    /// - Model fields: "Model" @ 0x007c1ca8, "ModelName" @ 0x007c1c8c, "ModelA" @ 0x007bf4bc, "ModelB" (implied)
    /// - "ModelType" @ 0x007c4568, "MODELTYPE" @ 0x007c036c, "ModelVariation" @ 0x007c0990
    /// - "ModelPart" @ 0x007bd42c, "ModelPart1" @ 0x007c0acc, "DefaultModel" @ 0x007c4530
    /// - "VisibleModel" @ 0x007c1c98, "refModel" @ 0x007babe8, "ProjModel" @ 0x007c31c0, "StuntModel" @ 0x007c37e0
    /// - "CameraModel" @ 0x007c3908, "MODEL01" @ 0x007c4b48, "MODEL02" @ 0x007c4b34, "MODEL03" @ 0x007c4b20
    /// - "MODELMIN02" @ 0x007c4b3c, "MODELMIN03" @ 0x007c4b28
    /// - Visibility: "VISIBLEVALUE" @ 0x007b6a58, "VisibleModel" @ 0x007c1c98, "IsBodyBagVisible" @ 0x007c1ff0
    /// - "sdr_invisible" @ 0x007cb1dc (invisibility shader/material)
    /// - Model loading: FUN_005261b0 @ 0x005261b0 (swkotor2.exe) loads creature model from appearance.2da row
    /// - Error messages: "CSWCCreature::LoadModel(): Failed to load creature model '%s'." @ 0x007c82fc (swkotor2.exe)
    /// - "CSWCCreatureAppearance::CreateBTypeBody(): Failed to load model '%s'." @ 0x007cdc40 (swkotor2.exe)
    /// - Original implementation: Entities with models can be rendered in the game world
    /// - ModelResRef: MDL file resource reference for 3D model (loaded from installation resources)
    /// - AppearanceRow: Index into appearance.2da for creature appearance customization (Appearance_Type field)
    /// - FUN_005261b0 loads creature model by reading appearance.2da row, extracting ModelA/ModelB fields,
    ///   constructing model path, loading MDL/MDX files (model geometry/animation)
    /// - Textures loaded from TPC files (texture data) referenced by TexA/TexB fields in appearance.2da
    /// - Appearance.2da defines: ModelA, ModelB (model variants), TexA, TexB (texture variants), Race (race model base)
    /// - Model visibility controlled by Visible flag (used for culling, stealth, invisibility effects)
    /// - IsLoaded flag tracks whether model data has been loaded into memory (for async loading)
    /// - Odyssey-specific: Uses MDL/MDX model format, TPC texture format, appearance.2da table structure
    /// </remarks>
    [PublicAPI]
    public class OdysseyRenderableComponent : BaseRenderableComponent
    {
        /// <summary>
        /// Initializes a new instance of the Odyssey renderable component.
        /// </summary>
        public OdysseyRenderableComponent()
            : base()
        {
        }

        /// <summary>
        /// Called when the model resource reference changes.
        /// Odyssey-specific: Triggers MDL model reloading.
        /// </summary>
        protected override void OnModelResRefChanged()
        {
            // Odyssey-specific: Model reloading would be handled by model loading system
            // MDL/MDX files would be reloaded when ModelResRef changes
            base.OnModelResRefChanged();
        }

        /// <summary>
        /// Called when the component is attached to an entity.
        /// </summary>
        public override void OnAttach()
        {
            base.OnAttach();
            // Odyssey-specific attachment logic can be added here if needed
        }

        /// <summary>
        /// Called when the component is detached from an entity.
        /// </summary>
        public override void OnDetach()
        {
            // Odyssey-specific: Clean up MDL model resources if needed
            base.OnDetach();
        }
    }
}


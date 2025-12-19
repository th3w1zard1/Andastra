namespace Andastra.Runtime.Core.Interfaces.Components
{
    /// <summary>
    /// Component for entities that can be rendered.
    /// </summary>
    /// <remarks>
    /// Renderable Component Interface:
    /// - TODO: lookup data from daorigins.exe/dragonage2.exe/masseffect.exe/masseffect2.exe/swkotor.exe/swkotor2.exe and split into subclass'd inheritence structures appropriately. parent class(es) should contain common code.
    /// - TODO: this should NOT specify swkotor2.exe unless it specifies the other exes as well!!!
    /// - Based on swkotor2.exe rendering system
    /// - Located via string references: Model loading and rendering functions handle entity models
    /// - "ModelResRef" @ 0x007c2f6c (model resource reference field), "Appearance_Type" @ 0x007c40f0 (appearance type field)
    /// - Model fields: "Model" @ 0x007c1ca8, "ModelName" @ 0x007c1c8c, "ModelA" @ 0x007bf4bc, "ModelB" (implied)
    /// - "ModelType" @ 0x007c4568, "MODELTYPE" @ 0x007c036c, "ModelVariation" @ 0x007c0990
    /// - "ModelPart" @ 0x007bd42c, "ModelPart1" @ 0x007c0acc, "DefaultModel" @ 0x007c4530
    /// - "VisibleModel" @ 0x007c1c98, "refModel" @ 0x007babe8, "ProjModel" @ 0x007c31c0, "StuntModel" @ 0x007c37e0
    /// - "CameraModel" @ 0x007c3908, "MODEL01" @ 0x007c4b48, "MODEL02" @ 0x007c4b34, "MODEL03" @ 0x007c4b20
    /// - "MODELMIN02" @ 0x007c4b3c, "MODELMIN03" @ 0x007c4b28
    /// - Visibility: "VISIBLEVALUE" @ 0x007b6a58, "VisibleModel" @ 0x007c1c98, "IsBodyBagVisible" @ 0x007c1ff0
    /// - "sdr_invisible" @ 0x007cb1dc (invisibility shader/material)
    /// - Model loading: FUN_005261b0 @ 0x005261b0 loads creature model from appearance.2da row
    /// - "CSWCCreature::LoadModel(): Failed to load creature model '%s'." @ 0x007c82fc (model loading error)
    /// - Original implementation: Entities with models can be rendered in the game world
    /// - ModelResRef: MDL file resource reference for 3D model (loaded from installation resources)
    /// - AppearanceRow: Index into appearance.2da for creature appearance customization (Appearance_Type field)
    /// - Visible: Controls whether entity is rendered (can be hidden for scripting/cutscenes, stealth effects, invisibility)
    /// - IsLoaded: Indicates whether model data has been loaded into memory (used for async loading optimization)
    /// - Models loaded from MDL/MDX files (model geometry/animation), textures from TPC files (texture data)
    /// - Appearance.2da defines: ModelA, ModelB (model variants), TexA, TexB (texture variants), Race (race model base)
    /// </remarks>
    public interface IRenderableComponent : IComponent
    {
        /// <summary>
        /// The model resource reference.
        /// </summary>
        string ModelResRef { get; set; }

        /// <summary>
        /// Whether the entity is currently visible.
        /// </summary>
        bool Visible { get; set; }

        /// <summary>
        /// Whether the model is currently loaded.
        /// </summary>
        bool IsLoaded { get; }

        /// <summary>
        /// The appearance row from appearance.2da (for creatures).
        /// </summary>
        int AppearanceRow { get; set; }
    }
}


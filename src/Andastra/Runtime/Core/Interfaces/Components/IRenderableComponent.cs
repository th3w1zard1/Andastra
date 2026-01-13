namespace Andastra.Runtime.Core.Interfaces.Components
{
    /// <summary>
    /// Component for entities that can be rendered.
    /// </summary>
    /// <remarks>
    /// Renderable Component Interface:
    /// - Cross-Engine Analysis (Reverse Engineered via Ghidra):
    ///   - Odyssey Engine (swkotor.exe, swkotor2.exe):
    ///     - swkotor.exe: CSWCCreature::LoadModel() @ 0x0074f85c loads creature model from appearance.2da
    ///     - swkotor2.exe: FUN_005261b0 @ 0x005261b0 loads creature model from appearance.2da row
    ///     - String references: "ModelResRef" @ 0x007c2f6c (model resource reference field), "Appearance_Type" @ 0x007c40f0 (appearance type field)
    ///     - Model fields: "Model" @ 0x007c1ca8, "ModelName" @ 0x007c1c8c, "ModelA" @ 0x007bf4bc, "ModelB" (implied)
    ///     - "ModelType" @ 0x007c4568, "MODELTYPE" @ 0x007c036c, "ModelVariation" @ 0x007c0990
    ///     - "ModelPart" @ 0x007bd42c, "ModelPart1" @ 0x007c0acc, "DefaultModel" @ 0x007c4530
    ///     - "VisibleModel" @ 0x007c1c98, "refModel" @ 0x007babe8, "ProjModel" @ 0x007c31c0, "StuntModel" @ 0x007c37e0
    ///     - "CameraModel" @ 0x007c3908, "MODEL01" @ 0x007c4b48, "MODEL02" @ 0x007c4b34, "MODEL03" @ 0x007c4b20
    ///     - "MODELMIN02" @ 0x007c4b3c, "MODELMIN03" @ 0x007c4b28
    ///     - Visibility: "VISIBLEVALUE" @ 0x007b6a58, "VisibleModel" @ 0x007c1c98, "IsBodyBagVisible" @ 0x007c1ff0
    ///     - "sdr_invisible" @ 0x007cb1dc (invisibility shader/material)
    ///     - Error messages: "CSWCCreature::LoadModel(): Failed to load creature model '%s'." @ 0x007c82fc (swkotor2.exe), @ 0x0074f85c (swkotor.exe)
    ///     - Model format: MDL/MDX files (model geometry/animation), textures from TPC files
    ///   - Aurora Engine (nwmain.exe, nwn2main.exe):
    ///     - nwmain.exe: CNWCCreature::LoadModel() loads creature models via CResRef
    ///     - nwmain.exe: CNWCItem::LoadModel, CNWCDoor::LoadModel, CNWCPlaceable::LoadModel - entity-specific loaders
    ///     - Model caching: Uses global _Models array to cache loaded models
    ///     - Model format: MDL files (similar to Odyssey), textures from TPC files
    ///     - Appearance system: Uses appearance.2da table similar to Odyssey
    ///   - Eclipse Engine (daorigins.exe, DragonAge2.exe):
    ///     - daorigins.exe: Model loading via Unreal Engine ResourceManager system
    ///     - DragonAge2.exe: Enhanced model loading with physics integration
    ///     - Model format: Engine-specific formats (PCC/UPK packages), textures from TEX/DDS files
    ///     - Appearance system: Uses appearance.2da table with Eclipse-specific columns
    /// - Common functionality across all engines:
    ///   - ModelResRef: Resource reference to 3D model file (format varies by engine)
    ///   - AppearanceRow: Index into appearance.2da for creature appearance customization (Appearance_Type field)
    ///   - Visible: Controls whether entity is rendered (can be hidden for scripting/cutscenes, stealth effects, invisibility)
    ///   - IsLoaded: Indicates whether model data has been loaded into memory (used for async loading optimization)
    ///   - Appearance.2da defines: ModelA, ModelB (model variants), TexA, TexB (texture variants), Race (race model base)
    /// - Implementation: BaseRenderableComponent (Runtime.Games.Common) provides common functionality
    ///   - Odyssey: OdysseyRenderableComponent : BaseRenderableComponent (Runtime.Games.Odyssey)
    ///   - Aurora: AuroraRenderableComponent : BaseRenderableComponent (Runtime.Games.Aurora)
    ///   - Eclipse: EclipseRenderableComponent : BaseRenderableComponent (Runtime.Games.Eclipse)
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

        /// <summary>
        /// The opacity/alpha value for rendering (0.0 = fully transparent, 1.0 = fully opaque).
        /// </summary>
        /// <remarks>
        /// Common across all engines:
        /// - Used for fade-in/fade-out effects (appear animation, destroy animation)
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FadeTime @ 0x007c60ec (fade duration), alpha blending for entity rendering
        /// - Rendering systems should apply this opacity value when rendering entities
        /// - Default value is 1.0 (fully opaque)
        /// - For appear animation: Starts at 0.0 and fades in to 1.0 over fade duration
        /// - For destroy animation: Starts at 1.0 and fades out to 0.0 over fade duration
        /// </remarks>
        float Opacity { get; set; }
    }
}


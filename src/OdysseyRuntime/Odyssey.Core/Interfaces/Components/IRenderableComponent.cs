namespace Odyssey.Core.Interfaces.Components
{
    /// <summary>
    /// Component for entities that can be rendered.
    /// </summary>
    /// <remarks>
    /// Renderable Component Interface:
    /// - Based on swkotor2.exe rendering system
    /// - Located via string references: Model loading and rendering functions handle entity models
    /// - "ModelResRef" @ 0x007c2f6c (model resource reference field), "Appearance_Type" @ 0x007c40f0 (appearance type field)
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


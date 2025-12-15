namespace Odyssey.Core.Interfaces.Components
{
    /// <summary>
    /// Component for entities that can be rendered.
    /// </summary>
    /// <remarks>
    /// Renderable Component Interface:
    /// - Based on swkotor2.exe rendering system
    /// - Located via string references: Model loading and rendering functions
    /// - Original implementation: Entities with models can be rendered in the game world
    /// - ModelResRef: MDL file resource reference for 3D model
    /// - AppearanceRow: Index into appearance.2da for creature appearance customization
    /// - Visible: Controls whether entity is rendered (can be hidden for scripting/cutscenes)
    /// - IsLoaded: Indicates whether model data has been loaded into memory
    /// - Models loaded from MDL/MDX files, textures from TPC files
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


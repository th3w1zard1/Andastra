namespace Odyssey.Core.Interfaces.Components
{
    /// <summary>
    /// Component for entities that can be rendered.
    /// </summary>
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


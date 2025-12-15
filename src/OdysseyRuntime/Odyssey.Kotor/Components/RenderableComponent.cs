using System;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;
using JetBrains.Annotations;

namespace Odyssey.Kotor.Components
{
    /// <summary>
    /// Component for entities that can be rendered with 3D models.
    /// </summary>
    /// <remarks>
    /// Renderable Component:
    /// - Based on swkotor2.exe rendering system
    /// - Located via string references: Model loading and rendering functions
    /// - Original implementation: Entities with models can be rendered in the game world
    /// - ModelResRef: MDL file resource reference for 3D model
    /// - AppearanceRow: Index into appearance.2da for creature appearance customization
    /// - Models loaded from MDL/MDX files, textures from TPC files
    /// - Based on swkotor2.exe: FUN_005261b0 @ 0x005261b0 (load creature model from appearance)
    /// </remarks>
    public class RenderableComponent : IRenderableComponent
    {
        private string _modelResRef;
        private bool _isLoaded;

        public IEntity Owner { get; set; }

        public void OnAttach() { }
        public void OnDetach() { }

        /// <summary>
        /// The model resource reference.
        /// </summary>
        public string ModelResRef
        {
            get { return _modelResRef; }
            set
            {
                if (_modelResRef != value)
                {
                    _modelResRef = value;
                    _isLoaded = false; // Model needs to be reloaded
                }
            }
        }

        /// <summary>
        /// Whether the entity is currently visible.
        /// </summary>
        public bool Visible { get; set; } = true;

        /// <summary>
        /// Whether the model is currently loaded.
        /// </summary>
        public bool IsLoaded
        {
            get { return _isLoaded; }
            set { _isLoaded = value; }
        }

        /// <summary>
        /// The appearance row from appearance.2da (for creatures).
        /// </summary>
        public int AppearanceRow { get; set; } = -1;
    }
}


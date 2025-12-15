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
    /// - Located via string references: "ModelResRef" @ 0x007c2f6c (model resource reference field)
    /// - "Appearance_Type" @ 0x007c40f0 (appearance type field for creatures)
    /// - Model loading: FUN_005261b0 @ 0x005261b0 loads creature model from appearance.2da row
    /// - Error messages: "CSWCCreature::LoadModel(): Failed to load creature model '%s'." @ 0x007c82fc
    /// - "CSWCCreatureAppearance::CreateBTypeBody(): Failed to load model '%s'." @ 0x007cdc40
    /// - Original implementation: Entities with models can be rendered in the game world
    /// - ModelResRef: MDL file resource reference for 3D model (loaded from installation resources)
    /// - AppearanceRow: Index into appearance.2da for creature appearance customization (Appearance_Type field)
    /// - FUN_005261b0 loads creature model by reading appearance.2da row, extracting ModelA/ModelB fields,
    ///   constructing model path, loading MDL/MDX files (model geometry/animation)
    /// - Textures loaded from TPC files (texture data) referenced by TexA/TexB fields in appearance.2da
    /// - Appearance.2da defines: ModelA, ModelB (model variants), TexA, TexB (texture variants), Race (race model base)
    /// - Model visibility controlled by Visible flag (used for culling, stealth, invisibility effects)
    /// - IsLoaded flag tracks whether model data has been loaded into memory (for async loading)
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


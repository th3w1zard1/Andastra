using System;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Common.Components
{
    /// <summary>
    /// Base implementation of renderable component functionality shared across all BioWare engines.
    /// </summary>
    /// <remarks>
    /// Base Renderable Component Implementation:
    /// - Common renderable properties and methods across all engines
    /// - Handles model resource references, visibility, loading state, and appearance data
    /// - Provides base for engine-specific renderable component implementations
    /// - Base classes MUST only contain functionality that is identical across ALL engines
    /// - Engine-specific details MUST be in subclasses
    /// - Cross-engine analysis:
    ///   - Odyssey: swkotor.exe, swkotor2.exe
    ///   - Aurora: nwmain.exe, nwn2main.exe
    ///   - Eclipse: daorigins.exe, DragonAge2.exe
    ///   - Infinity: MassEffect.exe, MassEffect2.exe
    /// 
    /// Common functionality across all engines:
    /// - ModelResRef: Resource reference to 3D model file (MDL format for Odyssey/Aurora, engine-specific formats for Eclipse/Infinity)
    /// - Visible: Controls whether entity is rendered (used for culling, stealth, invisibility effects)
    /// - IsLoaded: Indicates whether model data has been loaded into memory (used for async loading optimization)
    /// - AppearanceRow: Index into appearance.2da table for creature appearance customization
    /// - Model loading: All engines load models from resource files (MDL/MDX for Odyssey/Aurora, engine-specific for Eclipse/Infinity)
    /// - Appearance.2da: All engines use appearance.2da table to define creature appearance (ModelA/ModelB variants, TexA/TexB textures)
    /// - Visibility control: All engines support visibility toggling for scripting, cutscenes, stealth, invisibility effects
    /// - Async loading: All engines support async model loading with IsLoaded flag to track loading state
    /// 
    /// Engine-specific differences (handled in subclasses):
    /// - Model file format details (MDL/MDX vs engine-specific formats)
    /// - Texture file formats (TPC vs DDS vs engine-specific)
    /// - Appearance.2da column names and structure (may vary slightly)
    /// - Model loading implementation details (synchronous vs async patterns)
    /// - Memory management and caching strategies
    /// </remarks>
    [PublicAPI]
    public abstract class BaseRenderableComponent : IRenderableComponent
    {
        private string _modelResRef;
        private bool _isLoaded;
        private float _opacity;

        /// <summary>
        /// The entity this component is attached to.
        /// </summary>
        public IEntity Owner { get; set; }

        /// <summary>
        /// Initializes a new instance of the base renderable component.
        /// </summary>
        protected BaseRenderableComponent()
        {
            _modelResRef = string.Empty;
            _isLoaded = false;
            Visible = true;
            AppearanceRow = -1;
            _opacity = 1.0f; // Default to fully opaque
        }

        /// <summary>
        /// The model resource reference.
        /// </summary>
        /// <remarks>
        /// Common across all engines:
        /// - Odyssey: MDL file resource reference (loaded from installation resources)
        /// - Aurora: MDL file resource reference (loaded from installation resources)
        /// - Eclipse: Engine-specific model format resource reference
        /// - Infinity: Engine-specific model format resource reference
        /// </remarks>
        public virtual string ModelResRef
        {
            get { return _modelResRef; }
            set
            {
                if (_modelResRef != value)
                {
                    _modelResRef = value;
                    _isLoaded = false; // Model needs to be reloaded when ResRef changes
                    OnModelResRefChanged();
                }
            }
        }

        /// <summary>
        /// Whether the entity is currently visible.
        /// </summary>
        /// <remarks>
        /// Common across all engines:
        /// - Controls whether entity is rendered (can be hidden for scripting/cutscenes, stealth effects, invisibility)
        /// - Used by rendering systems for culling optimization
        /// - Can be toggled dynamically for gameplay effects (stealth, invisibility, scripting)
        /// </remarks>
        public virtual bool Visible { get; set; }

        /// <summary>
        /// Whether the model is currently loaded.
        /// </summary>
        /// <remarks>
        /// Common across all engines:
        /// - Indicates whether model data has been loaded into memory
        /// - Used for async loading optimization (prevents rendering unloaded models)
        /// - Set to false when ModelResRef changes (model needs to be reloaded)
        /// - Set to true when model loading completes
        /// </remarks>
        public virtual bool IsLoaded
        {
            get { return _isLoaded; }
            set { _isLoaded = value; }
        }

        /// <summary>
        /// The appearance row from appearance.2da (for creatures).
        /// </summary>
        /// <remarks>
        /// Common across all engines:
        /// - Index into appearance.2da table for creature appearance customization
        /// - Appearance.2da defines: ModelA, ModelB (model variants), TexA, TexB (texture variants), Race (race model base)
        /// - Used to construct model path and texture paths for creatures
        /// - -1 indicates no appearance data (for placeables, doors, etc. that use direct ModelResRef)
        /// </remarks>
        public virtual int AppearanceRow { get; set; }

        /// <summary>
        /// The opacity/alpha value for rendering (0.0 = fully transparent, 1.0 = fully opaque).
        /// </summary>
        /// <remarks>
        /// Common across all engines:
        /// - Used for fade-in/fade-out effects (appear animation, destroy animation)
        /// - Rendering systems should apply this opacity value when rendering entities
        /// - Default value is 1.0 (fully opaque)
        /// - For appear animation: Starts at 0.0 and fades in to 1.0 over fade duration
        /// - For destroy animation: Starts at 1.0 and fades out to 0.0 over fade duration
        /// </remarks>
        public virtual float Opacity
        {
            get { return _opacity; }
            set
            {
                // Clamp opacity to valid range [0.0, 1.0]
                _opacity = Math.Max(0.0f, Math.Min(1.0f, value));
            }
        }

        /// <summary>
        /// Called when the model resource reference changes.
        /// Engine-specific implementations can override to handle model reloading.
        /// </summary>
        protected virtual void OnModelResRefChanged()
        {
            // Base implementation does nothing - engine-specific implementations can override
            // to trigger model reloading, cleanup of old model data, etc.
        }

        /// <summary>
        /// Called when the component is attached to an entity.
        /// </summary>
        public virtual void OnAttach()
        {
            // Base implementation does nothing - engine-specific implementations can override
        }

        /// <summary>
        /// Called when the component is detached from an entity.
        /// </summary>
        public virtual void OnDetach()
        {
            // Base implementation does nothing - engine-specific implementations can override
            // to clean up model resources, cancel loading operations, etc.
        }
    }
}


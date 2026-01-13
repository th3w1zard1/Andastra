using System.Numerics;
using Andastra.Runtime.Core.Interfaces;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Common
{
    /// <summary>
    /// Base implementation of entity template factory shared across all BioWare engines.
    /// </summary>
    /// <remarks>
    /// Base Entity Template Factory Implementation:
    /// - Common entity template creation framework across all engines
    /// - Handles creation of entities from template resource references
    /// - Provides foundation for engine-specific template loading systems
    ///
    /// Based on reverse engineering of entity template factory systems across multiple BioWare engines:
    /// - Common concepts: Template resource references, entity creation from templates, position/facing
    /// - All engines that support template-based entity creation follow similar patterns
    /// - Template loading and entity creation are engine-specific implementations
    ///
    /// Common functionality across engines:
    /// - CreateCreatureFromTemplate: Creates creature entities from template ResRefs
    /// - Template resource reference validation and lookup
    /// - Entity positioning and orientation from parameters
    /// - Error handling for missing or invalid templates
    /// - Null return for failed template loading or entity creation
    ///
    /// Engine-specific differences:
    /// - Odyssey (swkotor.exe, swkotor2.exe): UTC GFF templates, EntityFactory wrapper
    ///   - swkotor.exe: FUN_0050a350 @ 0x0050a350 loads templates from GIT with TemplateResRef field
    ///   - swkotor2.exe: FUN_005261b0 @ 0x005261b0 loads creature templates, FUN_005fb0f0 @ 0x005fb0f0 loads template data
    ///   - Template loading: FUN_005fb0f0 @ 0x005fb0f0 loads creature templates from GFF, reads TemplateResRef field
    ///   - Located via string references: "TemplateResRef" @ 0x00747494 (swkotor.exe), "TemplateResRef" @ 0x007bd00c (swkotor2.exe)
    /// - Aurora (nwmain.exe): Similar template system, different GFF format
    ///   - Located via string references: "TemplateResRef" @ 0x140dddee8 (nwmain.exe)
    /// - Eclipse (daorigins.exe, DragonAge2.exe): TemplateResRef string exists but may use different system
    ///   - daorigins.exe: "TemplateResRef" @ 0x00af4f00 (no cross-references found, may use different approach)
    ///   - DragonAge2.exe: "TemplateResRef" @ 0x00bf2538 (no cross-references found, may use different approach)
    /// - Infinity (MassEffect.exe, MassEffect2.exe): No TemplateResRef support (different entity creation system)
    ///
    /// All engine-specific details (function addresses, GFF formats, implementation specifics) are in subclasses.
    /// This base class contains only functionality that is identical across ALL engines that support template-based entity creation.
    /// </remarks>
    [PublicAPI]
    public abstract class BaseEntityTemplateFactory : IEntityTemplateFactory
    {
        /// <summary>
        /// Creates a creature entity from a template ResRef at the specified position.
        /// </summary>
        /// <param name="templateResRef">The template resource reference (e.g., "n_darthmalak").</param>
        /// <param name="position">The spawn position.</param>
        /// <param name="facing">The facing direction in radians.</param>
        /// <returns>The created entity, or null if template not found or creation failed.</returns>
        /// <remarks>
        /// Common implementation pattern across all engines:
        /// - Validates templateResRef parameter (null/empty check)
        /// - Loads template from resource system (engine-specific)
        /// - Creates entity with ObjectId, ObjectType, position, facing
        /// - Applies template data to entity (engine-specific)
        /// - Returns null on failure (template not found, invalid data, creation error)
        ///
        /// Engine-specific implementations handle:
        /// - Template file format (GFF signatures, structure)
        /// - Resource loading (module archives, override directory)
        /// - Entity component initialization
        /// - Template data application to entity
        /// </remarks>
        public abstract IEntity CreateCreatureFromTemplate(string templateResRef, Vector3 position, float facing);

        /// <summary>
        /// Validates a template resource reference.
        /// </summary>
        /// <param name="templateResRef">The template resource reference to validate.</param>
        /// <returns>True if the template ResRef is valid, false otherwise.</returns>
        /// <remarks>
        /// Common validation across all engines:
        /// - ResRefs must not be null or empty
        /// - ResRefs must not exceed maximum length (typically 16 characters)
        /// - ResRefs should not contain invalid characters
        /// </remarks>
        protected static bool IsValidTemplateResRef([CanBeNull] string templateResRef)
        {
            if (string.IsNullOrEmpty(templateResRef))
            {
                return false;
            }

            // ResRefs are typically limited to 16 characters
            if (templateResRef.Length > 16)
            {
                return false;
            }

            return true;
        }
    }
}


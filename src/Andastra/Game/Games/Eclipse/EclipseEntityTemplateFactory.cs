using System.Numerics;
using Andastra.Parsing.Common;
using Andastra.Parsing.Installation;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Games.Common;
using Andastra.Runtime.Games.Eclipse.Loading;
using JetBrains.Annotations;

namespace Andastra.Runtime.Games.Eclipse
{
    /// <summary>
    /// Eclipse engine family implementation of entity template factory.
    /// Creates entities from templates using Eclipse-specific loading mechanisms.
    /// </summary>
    /// <remarks>
    /// Entity Template Factory (Eclipse Engine Family):
    /// - Common template factory implementation for Eclipse engine games (Dragon Age: Origins, Dragon Age 2)
    /// - Based on daorigins.exe and DragonAge2.exe entity creation systems
    /// - Located via string references: "TemplateResRef" @ 0x00af4f00 (daorigins.exe), "TemplateResRef" @ 0x00bf2538 (DragonAge2.exe)
    /// - Template loading: Eclipse uses UTC GFF templates (same format as Odyssey/Aurora)
    /// - Original implementation: Creates runtime entities from UTC GFF templates using EclipseEntityFactory
    /// - Module is required for template resource loading (UTC files from module archives)
    /// - Both games use identical template loading mechanism with same GFF format
    /// - This implementation wraps EclipseEntityFactory to provide Core-compatible interface
    /// </remarks>
    [PublicAPI]
    public class EclipseEntityTemplateFactory : BaseEntityTemplateFactory
    {
        private readonly EclipseEntityFactory _entityFactory;
        private readonly Module _module;

        /// <summary>
        /// Creates a new EclipseEntityTemplateFactory.
        /// </summary>
        /// <param name="entityFactory">The EclipseEntityFactory to use for creating entities.</param>
        /// <param name="module">The module to load templates from.</param>
        public EclipseEntityTemplateFactory(EclipseEntityFactory entityFactory, Module module)
        {
            _entityFactory = entityFactory ?? throw new System.ArgumentNullException("entityFactory");
            _module = module ?? throw new System.ArgumentNullException("module");
        }

        /// <summary>
        /// Creates a creature entity from a template ResRef at the specified position.
        /// </summary>
        /// <param name="templateResRef">The template resource reference (e.g., "n_darthmalak").</param>
        /// <param name="position">The spawn position.</param>
        /// <param name="facing">The facing direction in radians.</param>
        /// <returns>The created entity, or null if template not found or creation failed.</returns>
        /// <remarks>
        /// Eclipse engine family implementation:
        /// - Validates template ResRef using base class validation
        /// - Uses EclipseEntityFactory to load UTC GFF template and create entity
        /// - Based on daorigins.exe and DragonAge2.exe: EclipseEntityFactory.CreateCreatureFromTemplate loads UTC GFF and creates entity
        /// - Located via string references: "TemplateResRef" @ 0x00af4f00 (daorigins.exe), "TemplateResRef" @ 0x00bf2538 (DragonAge2.exe)
        /// - Original implementation: Loads UTC GFF, reads creature properties, creates EclipseEntity with components
        /// - Both games use identical template loading mechanism with same GFF format
        /// </remarks>
        public override IEntity CreateCreatureFromTemplate(string templateResRef, Vector3 position, float facing)
        {
            if (!IsValidTemplateResRef(templateResRef))
            {
                return null;
            }

            // Use EclipseEntityFactory to create creature from template
            // Based on daorigins.exe and DragonAge2.exe: EclipseEntityFactory.CreateCreatureFromTemplate loads UTC GFF and creates entity
            // Located via string references: "TemplateResRef" @ 0x00af4f00 (daorigins.exe), "TemplateResRef" @ 0x00bf2538 (DragonAge2.exe)
            // Original implementation: Loads UTC GFF, reads creature properties (Tag, FirstName, LastName, Appearance_Type,
            // FactionID, CurrentHitPoints, MaxHitPoints, Str/Dex/Con/Int/Wis/Cha, Scripts, Conversation), creates EclipseEntity with all components properly initialized
            return _entityFactory.CreateCreatureFromTemplate(_module, templateResRef, position, facing);
        }
    }
}


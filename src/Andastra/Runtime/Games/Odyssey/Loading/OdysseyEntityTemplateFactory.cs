using System.Numerics;
using Andastra.Parsing.Common;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Games.Common;

namespace Andastra.Runtime.Engines.Odyssey.Loading
{
    /// <summary>
    /// Odyssey engine family implementation of entity template factory.
    /// Creates entities from UTC templates using EntityFactory.
    /// </summary>
    /// <remarks>
    /// Entity Template Factory (Odyssey Engine Family):
    /// - Common template factory implementation for Odyssey engine games (KOTOR 1, KOTOR 2)
    /// - Based on swkotor.exe and swkotor2.exe entity creation systems
    /// - Located via string references: "TemplateResRef" @ 0x00747494 (swkotor.exe), "TemplateResRef" @ 0x007bd00c (swkotor2.exe)
    /// - Template loading: FUN_005fb0f0 @ 0x005fb0f0 (swkotor2.exe) loads creature templates from GFF
    /// - swkotor.exe: FUN_0050a350 @ 0x0050a350 loads templates from GIT with TemplateResRef field
    /// - swkotor2.exe: FUN_005261b0 @ 0x005261b0 loads creature templates, FUN_005fb0f0 @ 0x005fb0f0 loads template data
    /// - Original implementation: Creates runtime entities from UTC GFF templates
    /// - This implementation wraps EntityFactory to provide Core-compatible interface
    /// - Module is required for template resource loading (UTC files from module archives)
    /// - Both games use identical template loading mechanism with different function addresses
    /// </remarks>
    public class OdysseyEntityTemplateFactory : BaseEntityTemplateFactory
    {
        private readonly EntityFactory _entityFactory;
        private readonly Module _module;

        /// <summary>
        /// Creates a new OdysseyEntityTemplateFactory.
        /// </summary>
        /// <param name="entityFactory">The EntityFactory to use for creating entities.</param>
        /// <param name="module">The module to load templates from.</param>
        public OdysseyEntityTemplateFactory(EntityFactory entityFactory, Module module)
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
        /// Odyssey engine family implementation:
        /// - Validates template ResRef using base class validation
        /// - Uses EntityFactory to load UTC GFF template and create entity
        /// - Based on swkotor.exe and swkotor2.exe: EntityFactory.CreateCreatureFromTemplate loads UTC GFF and creates entity
        /// - Located via string references: "TemplateResRef" @ 0x00747494 (swkotor.exe), "TemplateResRef" @ 0x007bd00c (swkotor2.exe)
        /// - Original implementation: Loads UTC GFF, reads creature properties, creates entity with components
        /// - Both games use identical template loading mechanism
        /// </remarks>
        public override IEntity CreateCreatureFromTemplate(string templateResRef, Vector3 position, float facing)
        {
            if (!IsValidTemplateResRef(templateResRef))
            {
                return null;
            }

            // Use EntityFactory to create creature from template
            // Based on swkotor.exe and swkotor2.exe: EntityFactory.CreateCreatureFromTemplate loads UTC GFF and creates entity
            // Located via string references: "TemplateResRef" @ 0x00747494 (swkotor.exe), "TemplateResRef" @ 0x007bd00c (swkotor2.exe)
            // Original implementation: Loads UTC GFF, reads creature properties, creates entity with components
            return _entityFactory.CreateCreatureFromTemplate(_module, templateResRef, position, facing);
        }
    }
}


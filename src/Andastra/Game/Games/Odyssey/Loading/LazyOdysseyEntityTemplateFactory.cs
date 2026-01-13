using System.Numerics;
using BioWare.NET.Common;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Game.Games.Common;

namespace Andastra.Game.Games.Odyssey.Loading
{
    /// <summary>
    /// Lazy-loading Odyssey entity template factory that retrieves the module from ModuleLoader on demand.
    /// This allows the factory to be created before a module is loaded, making it suitable for use
    /// in GameSession constructor where the module isn't available yet.
    /// </summary>
    /// <remarks>
    /// Lazy Entity Template Factory (Odyssey Engine Family):
    /// - Created to solve the chicken-and-egg problem: GameSession needs PartySystem, PartySystem needs template factory,
    ///   but template factory needs module, which isn't loaded until later. This implementation uses lazy-loading to retrieve
    ///   the module from ModuleLoader on-demand when CreateCreatureFromTemplate is called, allowing the factory to be created
    ///   before the module is loaded. This matches the original engine behavior where template creation requires module resources.
    /// - Based on swkotor.exe and swkotor2.exe entity creation systems
    /// - Located via string references: "TemplateResRef" @ 0x00747494 (swkotor.exe), "TemplateResRef" @ 0x007bd00c (swkotor2.exe)
    /// - Template loading: 0x005fb0f0 @ 0x005fb0f0 (swkotor2.exe) loads creature templates from GFF
    /// - swkotor.exe: 0x0050a350 @ 0x0050a350 loads templates from GIT with TemplateResRef field
    /// - swkotor2.exe: 0x005261b0 @ 0x005261b0 loads creature templates, 0x005fb0f0 @ 0x005fb0f0 loads template data
    /// - Original implementation: Creates runtime entities from UTC GFF templates
    /// - This implementation wraps EntityFactory to provide Core-compatible interface
    /// - Module is retrieved lazily from ModuleLoader when CreateCreatureFromTemplate is called
    /// - Returns null if module is not loaded when template creation is attempted
    /// - Both games use identical template loading mechanism with different function addresses
    /// </remarks>
    public class LazyOdysseyEntityTemplateFactory : BaseEntityTemplateFactory
    {
        private readonly EntityFactory _entityFactory;
        private readonly ModuleLoader _moduleLoader;

        /// <summary>
        /// Creates a new LazyOdysseyEntityTemplateFactory.
        /// </summary>
        /// <param name="entityFactory">The EntityFactory to use for creating entities.</param>
        /// <param name="moduleLoader">The ModuleLoader to retrieve the current module from.</param>
        public LazyOdysseyEntityTemplateFactory(EntityFactory entityFactory, ModuleLoader moduleLoader)
        {
            _entityFactory = entityFactory ?? throw new System.ArgumentNullException("entityFactory");
            _moduleLoader = moduleLoader ?? throw new System.ArgumentNullException("moduleLoader");
        }

        /// <summary>
        /// Creates a creature entity from a template ResRef at the specified position.
        /// </summary>
        /// <param name="templateResRef">The template resource reference (e.g., "n_darthmalak").</param>
        /// <param name="position">The spawn position.</param>
        /// <param name="facing">The facing direction in radians.</param>
        /// <returns>The created entity, or null if template not found, creation failed, or module not loaded.</returns>
        /// <remarks>
        /// Lazy-loading implementation:
        /// - Validates template ResRef using base class validation
        /// - Retrieves current module from ModuleLoader (lazy loading)
        /// - Returns null if module is not loaded
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

            // Get current module from module loader (lazy loading)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Module must be loaded before entities can be created from templates
            // Located via string references: Module loading precedes entity template loading
            // Original implementation: Template creation requires module to be loaded for resource access
            BioWare.NET.Common.Module module = _moduleLoader?.GetCurrentModule();
            if (module == null)
            {
                // Module not loaded yet - this is expected before LoadModuleAsync completes
                // PartySystem will handle this gracefully by creating basic entities as fallback
                return null;
            }

            // Use EntityFactory to create creature from template
            // Based on swkotor.exe and swkotor2.exe: EntityFactory.CreateCreatureFromTemplate loads UTC GFF and creates entity
            // Located via string references: "TemplateResRef" @ 0x00747494 (swkotor.exe), "TemplateResRef" @ 0x007bd00c (swkotor2.exe)
            // Original implementation: Loads UTC GFF, reads creature properties, creates entity with components
            return _entityFactory.CreateCreatureFromTemplate(module, templateResRef, position, facing);
        }
    }
}

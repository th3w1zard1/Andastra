using System.Numerics;
using Andastra.Runtime.Core.Enums;

namespace Andastra.Runtime.Core.Interfaces
{
    /// <summary>
    /// Factory for creating entities from template resource references.
    /// </summary>
    /// <remarks>
    /// Entity Template Factory Interface:
    /// - Core interface for entity template creation across all BioWare engines
    /// - Implemented by engine-specific template factories through three-tier inheritance structure
    /// - Tier 1 (Base): BaseEntityTemplateFactory (Runtime.Games.Common) - Common functionality across all engines
    /// - Tier 2 (Engine): OdysseyEntityTemplateFactory, AuroraEntityTemplateFactory, EclipseEntityTemplateFactory - Engine family implementations
    /// - Tier 3 (Game): Game-specific implementations inherit from engine subclasses when needed
    ///
    /// Based on reverse engineering of entity template factory systems across multiple BioWare engines:
    /// - Odyssey (swkotor.exe, swkotor2.exe): UTC GFF templates, EntityFactory wrapper
    ///   - swkotor.exe: 0x0050a350 @ 0x0050a350 loads templates from GIT with TemplateResRef field
    ///   - swkotor2.exe: 0x005261b0 @ 0x005261b0 loads creature templates, 0x005fb0f0 @ 0x005fb0f0 loads template data
    ///   - Located via string references: "TemplateResRef" @ 0x00747494 (swkotor.exe), "TemplateResRef" @ 0x007bd00c (swkotor2.exe)
    /// - Aurora (nwmain.exe): Similar template system, different GFF format
    ///   - Located via string references: "TemplateResRef" @ 0x140dddee8 (nwmain.exe)
    /// - Eclipse (daorigins.exe, DragonAge2.exe): TemplateResRef string exists but may use different system
    ///   - daorigins.exe: "TemplateResRef" @ 0x00af4f00 (no cross-references found, may use different approach)
    ///   - DragonAge2.exe: "TemplateResRef" @ 0x00bf2538 (no cross-references found, may use different approach)
    /// - Infinity (MassEffect.exe, MassEffect2.exe): No TemplateResRef support (different entity creation system)
    ///
    /// Original implementation: Creates runtime entities from GFF templates (UTC, UTP, UTD, etc.)
    /// Templates define entity properties, stats, scripts, appearance
    /// This interface allows Core layer to create entities without depending on game-specific implementations
    /// Engine-specific layers (Odyssey, Aurora, Eclipse) implement this interface through BaseEntityTemplateFactory
    /// </remarks>
    public interface IEntityTemplateFactory
    {
        /// <summary>
        /// Creates a creature entity from a template ResRef at the specified position.
        /// </summary>
        /// <param name="templateResRef">The template resource reference (e.g., "n_darthmalak").</param>
        /// <param name="position">The spawn position.</param>
        /// <param name="facing">The facing direction in radians.</param>
        /// <returns>The created entity, or null if template not found or creation failed.</returns>
        IEntity CreateCreatureFromTemplate(string templateResRef, Vector3 position, float facing);
    }
}


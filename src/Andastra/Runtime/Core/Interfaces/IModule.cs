using System.Collections.Generic;

namespace Andastra.Runtime.Core.Interfaces
{
    /// <summary>
    /// Represents a game module (collection of areas and global state).
    /// </summary>
    /// <remarks>
    /// Module Interface:
    /// - Common interface shared across all BioWare engines (Odyssey, Aurora, Eclipse, Infinity)
    /// - Defines the core contract for module representation that all engines must provide
    /// - Engine-specific implementations are in concrete classes (OdysseyModule, AuroraModule, EclipseModule, InfinityModule)
    /// - Common concepts across all engines:
    ///   - Module definition with entry area and position
    ///   - Area management within modules
    ///   - Time management (dawn/dusk hours, calendar)
    ///   - Script hooks for module events
    ///   - Resource reference (ResRef) for module identification
    /// - Engine-specific details (file formats, loading functions, etc.) are in implementation classes:
    ///   - Odyssey (swkotor.exe, swkotor2.exe): IFO file format, FUN_00708990 @ 0x00708990 (swkotor2.exe)
    ///   - Aurora (nwmain.exe): Module.ifo format, CNWSModule::LoadModule
    ///   - Eclipse (daorigins.exe, DragonAge2.exe, , ): UnrealScript-based module loading
    ///   - Infinity (.exe, .exe, .exe): ARE/WED/GAM file formats
    /// - Based on cross-engine analysis of module systems in all BioWare engines
    /// </remarks>
    public interface IModule
    {
        /// <summary>
        /// The resource reference name of this module.
        /// </summary>
        string ResRef { get; }

        /// <summary>
        /// The display name of the module.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// The entry area of the module.
        /// </summary>
        string EntryArea { get; }

        /// <summary>
        /// Gets all areas in this module.
        /// </summary>
        IEnumerable<IArea> Areas { get; }

        /// <summary>
        /// Gets an area by its resource reference.
        /// </summary>
        IArea GetArea(string resRef);

        /// <summary>
        /// Gets the script to run for a module event.
        /// </summary>
        string GetScript(Enums.ScriptEvent eventType);

        /// <summary>
        /// The dawn hour (0-23).
        /// </summary>
        int DawnHour { get; }

        /// <summary>
        /// The dusk hour (0-23).
        /// </summary>
        int DuskHour { get; }

        /// <summary>
        /// Current game time - minutes past midnight.
        /// </summary>
        int MinutesPastMidnight { get; set; }

        /// <summary>
        /// Current game calendar day.
        /// </summary>
        int Day { get; set; }

        /// <summary>
        /// Current game calendar month.
        /// </summary>
        int Month { get; set; }

        /// <summary>
        /// Current game calendar year.
        /// </summary>
        int Year { get; set; }
    }
}


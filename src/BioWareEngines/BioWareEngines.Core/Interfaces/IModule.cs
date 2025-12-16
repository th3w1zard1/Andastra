using System.Collections.Generic;

namespace BioWareEngines.Core.Interfaces
{
    /// <summary>
    /// Represents a game module (collection of areas and global state).
    /// </summary>
    /// <remarks>
    /// Module Interface:
    /// - Based on swkotor2.exe module system
    /// - Located via string references: "Mod_" prefix for module fields (Mod_Tag, Mod_Entry_Area, etc.)
    /// - IFO file format: GFF with "IFO " signature containing module metadata
    /// - Module loading: FUN_00708990 @ 0x00708990 (loads module, sets up areas, spawns entities)
    /// - Original implementation stores module data in IFO file, references areas by ResRef
    /// - Module contains entry area, entry position, time settings, and script hooks
    /// - Based on IFO file format documentation in vendor/PyKotor/wiki/GFF-IFO.md
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


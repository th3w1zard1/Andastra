using JetBrains.Annotations;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Games.Common
{
    /// <summary>
    /// Engine-agnostic interface for accessing game data tables.
    /// </summary>
    /// <remarks>
    /// Game Data Provider Interface:
    /// - Provides abstraction for accessing game data across all engines
    /// - Common functionality: Lookup creature properties (radius, speed, etc.) from game data tables
    /// - Engine-specific implementations:
    ///   - Odyssey (swkotor.exe, swkotor2.exe): Uses 2DA tables (appearance.2da) via GameDataManager
    ///   - Aurora (nwmain.exe): Uses C2DA class for 2DA table access via AuroraTwoDATableManager
    ///   - Eclipse (daorigins.exe, DragonAge2.exe): Uses 2DA tables via EclipseTwoDATableManager
    ///   - Infinity: Uses 2DA tables (similar to Eclipse)
    /// - Based on reverse engineering:
    ///   - swkotor2.exe: FUN_0041d2c0 @ 0x0041d2c0 (2DA table lookup), FUN_0065a380 @ 0x0065a380 (GetCreatureRadius)
    ///   - swkotor.exe: Similar 2DA lookup system
    ///   - nwmain.exe: C2DA::Load2DArray @ 0x1401a73a0 for 2DA loading
    /// - Common pattern: All engines store creature properties (radius, speed, etc.) in data tables indexed by appearance type
    /// 
    /// This interface extends Core.Interfaces.IGameDataProvider to maintain compatibility.
    /// The interface definition was moved to Core.Interfaces to avoid circular dependencies.
    /// All existing implementations remain compatible as they automatically implement the base interface.
    /// </remarks>
    [PublicAPI]
    public interface IGameDataProvider : Core.Interfaces.IGameDataProvider
    {
        /// <summary>
        /// Gets the creature collision radius for a given appearance type.
        /// </summary>
        /// <param name="appearanceType">The appearance type index (row index in appearance.2da for Odyssey/Aurora).</param>
        /// <param name="defaultRadius">Default radius to return if lookup fails.</param>
        /// <returns>The creature collision radius, or defaultRadius if lookup fails.</returns>
        /// <remarks>
        /// Based on reverse engineering:
        /// - swkotor2.exe: FUN_0065a380 @ 0x0065a380 (GetCreatureRadius) calls FUN_0041d2c0 to lookup "hitradius" from appearance.2da
        /// - swkotor.exe: Similar implementation
        /// - nwmain.exe: Uses C2DA class to lookup hitradius from appearance.2da
        /// - Eclipse/Infinity: Uses EclipseTwoDATableManager to lookup hitradius from appearance.2da (same pattern as Odyssey/Aurora)
        /// - Original implementation: Looks up "hitradius" column from appearance.2da table using appearanceType as row index
        /// - Falls back to size-based defaults if appearance data unavailable:
        ///   - Size 0 (Small): 0.3
        ///   - Size 1 (Medium): 0.5 (default)
        ///   - Size 2 (Large): 0.7
        ///   - Size 3 (Huge): 1.0
        ///   - Size 4 (Gargantuan): 1.5
        /// </remarks>
        // Methods are inherited from Core.Interfaces.IGameDataProvider
    }
}


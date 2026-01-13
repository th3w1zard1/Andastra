using JetBrains.Annotations;

namespace Andastra.Game.Games.Common
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
    /// - Based on reverse engineering of multiple BioWare engines.
    /// - Common pattern: All engines store creature properties (radius, speed, etc.) in data tables indexed by appearance type
    /// 
    /// This interface extends Core.Interfaces.IGameDataProvider to maintain compatibility.
    /// The interface definition was moved to Core.Interfaces to avoid circular dependencies.
    /// All existing implementations remain compatible as they automatically implement the base interface.
    /// </remarks>
    [PublicAPI]
    public interface IGameDataProvider : Runtime.Core.Interfaces.IGameDataProvider
    {
        /// <summary>
        /// Gets the creature collision radius for a given appearance type.
        /// </summary>
        /// <param name="appearanceType">The appearance type index (row index in appearance.2da for Odyssey/Aurora).</param>
        /// <param name="defaultRadius">Default radius to return if lookup fails.</param>
        /// <returns>The creature collision radius, or defaultRadius if lookup fails.</returns>
        /// <remarks>
        /// Common pattern across all engines:
        /// - Looks up "hitradius" column from appearance.2da table using appearanceType as row index
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


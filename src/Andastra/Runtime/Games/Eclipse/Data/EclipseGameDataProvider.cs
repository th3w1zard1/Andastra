using JetBrains.Annotations;
using Andastra.Runtime.Games.Common;

namespace Andastra.Runtime.Games.Eclipse.Data
{
    /// <summary>
    /// Eclipse-specific implementation of IGameDataProvider.
    /// </summary>
    /// <remarks>
    /// Eclipse Game Data Provider:
    /// - Based on daorigins.exe, DragonAge2.exe, MassEffect.exe, MassEffect2.exe
    /// - Eclipse engine uses UnrealScript-based data access ("Get2DAValue", "GetNum2DARows" functions)
    /// - Original implementation: UnrealScript-based 2DA system (different architecture than Odyssey/Aurora)
    /// - TODO: Implement full Eclipse 2DA table access when Eclipse engine is fully implemented
    /// </remarks>
    [PublicAPI]
    public class EclipseGameDataProvider : IGameDataProvider
    {
        /// <summary>
        /// Gets the creature collision radius for a given appearance type.
        /// </summary>
        /// <param name="appearanceType">The appearance type index.</param>
        /// <param name="defaultRadius">Default radius to return if lookup fails.</param>
        /// <returns>The creature collision radius, or defaultRadius if lookup fails.</returns>
        /// <remarks>
        /// Based on Eclipse engine: Uses UnrealScript-based data access
        /// - TODO: Implement full Eclipse data lookup when Eclipse engine is fully implemented
        /// - For now, returns default radius
        /// </remarks>
        public float GetCreatureRadius(int appearanceType, float defaultRadius = 0.5f)
        {
            // TODO: PLACEHOLDER - Implement Eclipse data lookup
            // Eclipse uses UnrealScript-based data access
            // For now, return default radius
            return defaultRadius;
        }

        /// <summary>
        /// Gets a float value from a game data table.
        /// </summary>
        /// <param name="tableName">The table name.</param>
        /// <param name="rowIndex">The row index.</param>
        /// <param name="columnName">The column name (case-insensitive).</param>
        /// <param name="defaultValue">Default value to return if lookup fails.</param>
        /// <returns>The float value, or defaultValue if lookup fails.</returns>
        /// <remarks>
        /// Based on Eclipse engine: Uses UnrealScript-based data access
        /// - TODO: Implement full Eclipse data lookup when Eclipse engine is fully implemented
        /// </remarks>
        public float GetTableFloat(string tableName, int rowIndex, string columnName, float defaultValue = 0.0f)
        {
            // TODO: PLACEHOLDER - Implement Eclipse data lookup
            // Eclipse uses UnrealScript-based data access
            // For now, return default value
            return defaultValue;
        }
    }
}


using Andastra.Runtime.Core;
using Andastra.Runtime.Scripting.Interfaces;

namespace Andastra.Runtime.Scripting.VM
{
    /// <summary>
    /// Factory for creating game-specific script globals instances.
    /// </summary>
    /// <remarks>
    /// Script Globals Factory:
    /// - Based on swkotor.exe and swkotor2.exe script variable system initialization
    /// - Creates appropriate script globals implementation based on game type (K1 or K2)
    /// - Factory pattern ensures correct script globals instance is created for each game
    /// - Original implementation: Game-specific initialization may differ between K1 and K2
    /// - K1: Uses K1ScriptGlobals (based on swkotor.exe)
    /// - K2: Uses K2ScriptGlobals (based on swkotor2.exe)
    /// </remarks>
    public static class ScriptGlobalsFactory
    {
        /// <summary>
        /// Creates a script globals instance for the specified game type.
        /// </summary>
        /// <param name="game">The KOTOR game type (K1 or K2).</param>
        /// <returns>Script globals instance appropriate for the game type.</returns>
        /// <remarks>
        /// Script Globals Creation:
        /// - Based on swkotor.exe and swkotor2.exe: Script globals system initializes global variables
        /// - Original implementation: Global variables persist across saves, initialized at game start
        /// - K1: Returns K1ScriptGlobals instance (based on swkotor.exe)
        /// - K2: Returns K2ScriptGlobals instance (based on swkotor2.exe)
        /// - Factory pattern ensures correct implementation is used for each game
        /// </remarks>
        public static IScriptGlobals Create(KotorGame game)
        {
            if (game == KotorGame.K1)
            {
                return new K1ScriptGlobals();
            }
            else if (game == KotorGame.K2)
            {
                return new K2ScriptGlobals();
            }
            else
            {
                // Default to K2 for unknown game types (matches original base ScriptGlobals behavior)
                return new K2ScriptGlobals();
            }
        }
    }
}

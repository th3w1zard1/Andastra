using System;
using System.Collections.Generic;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Engines.Common;

namespace Andastra.Game.Engines.Aurora.Profiles
{
    /// <summary>
    /// Factory for creating Aurora Engine game profiles based on game type.
    /// </summary>
    /// <remarks>
    /// Aurora Profile Factory:
    /// - Based on Aurora Engine game profile system (Neverwinter Nights series)
    /// - Located via string references: Game version detection determines which profile to use (NWN vs NWN2)
    /// - Resource setup: nwmain.exe/nwn2main.exe sets up resource directories and precedence
    /// - Original implementation: Factory pattern for creating game-specific profiles
    /// - Game detection: Determines game type from installation directory (checks for nwmain.exe vs nwn2main.exe)
    /// - Profile creation: Returns NeverwinterNightsGameProfile for NWN, NeverwinterNights2GameProfile for NWN2
    /// - Based on Aurora Engine's game profile system initialized by CExoResMan
    /// - nwmain.exe/nwn2main.exe: "CExoResMan::Initialize" @ addresses in executables sets up all game directories
    /// </remarks>
    public static class AuroraProfileFactory
    {
        private static readonly Dictionary<GameType, Func<IEngineProfile>> _profileFactories;

        static AuroraProfileFactory()
        {
            _profileFactories = new Dictionary<GameType, Func<IEngineProfile>>
            {
                { GameType.NWN, () => new NeverwinterNightsGameProfile() },
                { GameType.NWN2, () => new NeverwinterNights2GameProfile() }
            };
        }

        /// <summary>
        /// Creates a game profile for the specified game type.
        /// </summary>
        /// <param name="gameType">The type of game.</param>
        /// <returns>A game profile instance.</returns>
        /// <exception cref="NotSupportedException">Thrown when the game type is not supported.</exception>
        public static IEngineProfile CreateProfile(GameType gameType)
        {
            if (_profileFactories.TryGetValue(gameType, out Func<IEngineProfile> factory))
            {
                return factory();
            }
            throw new NotSupportedException("Aurora Engine game type not supported: " + gameType);
        }

        /// <summary>
        /// Gets all available Aurora Engine game profiles.
        /// </summary>
        public static IEnumerable<IEngineProfile> GetAllProfiles()
        {
            foreach (Func<IEngineProfile> factory in _profileFactories.Values)
            {
                yield return factory();
            }
        }

        /// <summary>
        /// Checks if a game type is supported.
        /// </summary>
        public static bool IsSupported(GameType gameType)
        {
            return _profileFactories.ContainsKey(gameType);
        }

        /// <summary>
        /// Registers a custom Aurora Engine game profile factory.
        /// </summary>
        /// <remarks>
        /// This allows extending the engine with additional game profiles
        /// for other Aurora Engine games in the future.
        /// </remarks>
        public static void RegisterProfile(GameType gameType, Func<IEngineProfile> factory)
        {
            _profileFactories[gameType] = factory;
        }
    }
}

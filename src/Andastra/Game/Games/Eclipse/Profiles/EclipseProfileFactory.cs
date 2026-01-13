using System;
using System.Collections.Generic;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Game.Games.Common;
using Andastra.Game.Games.Common;

namespace Andastra.Game.Games.Engines.Eclipse.Profiles
{
    /// <summary>
    /// Factory for creating Eclipse Engine game profiles based on game type.
    /// </summary>
    /// <remarks>
    /// Eclipse Profile Factory:
    /// - Based on Eclipse Engine game profile system (Dragon Age series)
    /// - Located via string references: Game version detection determines which profile to use (DA:O vs DA2)
    /// - Resource setup: daorigins.exe/DragonAge2.exe sets up resource directories and precedence
    /// - Original implementation: Factory pattern for creating game-specific profiles
    /// - Game detection: Determines game type from installation directory (checks for daorigins.exe vs DragonAge2.exe)
    /// - Profile creation: Returns DragonAgeOriginsGameProfile for DA:O, DragonAge2GameProfile for DA2
    /// - Based on Eclipse Engine's game profile system initialized by resource manager
    /// </remarks>
    public static class EclipseProfileFactory
    {
        private static readonly Dictionary<GameType, Func<IEngineProfile>> _profileFactories;

        static EclipseProfileFactory()
        {
            _profileFactories = new Dictionary<GameType, Func<IEngineProfile>>
            {
                { GameType.DA_ORIGINS, () => new DragonAgeOriginsGameProfile() },
                { GameType.DA2, () => new DragonAge2GameProfile() }
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
            throw new NotSupportedException("Eclipse Engine game type not supported: " + gameType);
        }

        /// <summary>
        /// Gets all available Eclipse Engine game profiles.
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
        /// Registers a custom Eclipse Engine game profile factory.
        /// </summary>
        /// <remarks>
        /// This allows extending the engine with additional game profiles
        /// for other Eclipse Engine games in the future.
        /// </remarks>
        public static void RegisterProfile(GameType gameType, Func<IEngineProfile> factory)
        {
            _profileFactories[gameType] = factory;
        }
    }
}

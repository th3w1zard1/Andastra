using System;
using System.Collections.Generic;
using BioWare.NET;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Game.Games.Common;

namespace Andastra.Game.Games.Odyssey.Profiles
{
    /// <summary>
    /// Factory for creating game profiles based on game type.
    /// </summary>
    /// <remarks>
    /// Game Profile Factory:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) game profile system
    /// - Located via string references: Game version detection determines which profile to use (K1 vs K2)
    /// - Resource setup: 0x00633270 @ 0x00633270 sets up resource directories and precedence (related to game profile configuration)
    /// - Original implementation: Factory pattern for creating game-specific profiles
    /// - Game detection: Determines game type from installation directory (checks for swkotor.exe vs swkotor2.exe)
    /// - Profile creation: Returns K1GameProfile for KOTOR 1, K2GameProfile for KOTOR 2
    /// - Note: This is a factory pattern abstraction, but relates to game profile system initialized by 0x00633270
    /// </remarks>
    public static class GameProfileFactory
    {
        private static readonly Dictionary<GameType, Func<Andastra.Game.Games.Common.IEngineProfile>> _profileFactories;

        static GameProfileFactory()
        {
            _profileFactories = new Dictionary<GameType, Func<Andastra.Game.Games.Common.IEngineProfile>>
            {
                { GameType.K1, () => new OdysseyKotor1GameProfile() },
                { GameType.K2, () => new OdysseyTheSithLordsGameProfile() }
            };
        }

        /// <summary>
        /// Creates a game profile for the specified game type.
        /// </summary>
        /// <param name="gameType">The type of game.</param>
        /// <returns>A game profile instance.</returns>
        /// <exception cref="NotSupportedException">Thrown when the game type is not supported.</exception>
        public static Andastra.Game.Games.Common.IEngineProfile CreateProfile(GameType gameType)
        {
            if (_profileFactories.TryGetValue(gameType, out Func<Andastra.Game.Games.Common.IEngineProfile> factory))
            {
                return factory();
            }
            throw new NotSupportedException("Game type not supported: " + gameType);
        }

        /// <summary>
        /// Gets all available game profiles.
        /// </summary>
        public static IEnumerable<Andastra.Game.Games.Common.IEngineProfile> GetAllProfiles()
        {
            foreach (Func<Andastra.Game.Games.Common.IEngineProfile> factory in _profileFactories.Values)
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
        /// Registers a custom game profile factory.
        /// </summary>
        /// <remarks>
        /// This allows extending the engine with additional game profiles
        /// for other Aurora-family games in the future.
        /// </remarks>

        public static void RegisterProfile(GameType gameType, Func<Andastra.Game.Games.Common.IEngineProfile> factory)
        {
            _profileFactories[gameType] = factory;
        }
    }
}

using System;
using System.Collections.Generic;
using CSharpKOTOR.Common;

namespace Odyssey.Kotor.Profiles
{
    /// <summary>
    /// Factory for creating game profiles based on game type.
    /// </summary>
    public static class GameProfileFactory
    {
        private static readonly Dictionary<GameType, Func<IGameProfile>> _profileFactories;
        
        static GameProfileFactory()
        {
            _profileFactories = new Dictionary<GameType, Func<IGameProfile>>
            {
                { GameType.K1, () => new K1GameProfile() },
                { GameType.TSL, () => new K2GameProfile() }
            };
        }
        
        /// <summary>
        /// Creates a game profile for the specified game type.
        /// </summary>
        /// <param name="gameType">The type of game.</param>
        /// <returns>A game profile instance.</returns>
        /// <exception cref="NotSupportedException">Thrown when the game type is not supported.</exception>
        public static IGameProfile CreateProfile(GameType gameType)
        {
            if (_profileFactories.TryGetValue(gameType, out var factory))
            {
                return factory();
            }
            throw new NotSupportedException("Game type not supported: " + gameType);
        }
        
        /// <summary>
        /// Gets all available game profiles.
        /// </summary>
        public static IEnumerable<IGameProfile> GetAllProfiles()
        {
            foreach (var factory in _profileFactories.Values)
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
        public static void RegisterProfile(GameType gameType, Func<IGameProfile> factory)
        {
            _profileFactories[gameType] = factory;
        }
    }
}

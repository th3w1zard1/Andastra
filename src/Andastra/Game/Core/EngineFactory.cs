using System;
using BioWare.NET.Common;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Game.Games.Common;
using Andastra.Game.Games.Aurora;
using Andastra.Game.Games.Aurora.Profiles;
using Andastra.Game.Games.Common;
using Andastra.Game.Games.Eclipse.DragonAge2;
using Andastra.Game.Games.Eclipse.DragonAgeOrigins;
using DragonAgeOriginsEngine = Andastra.Game.Games.Engines.Eclipse.DragonAgeOrigins.DragonAgeOriginsEngine;
using DragonAge2Engine = Andastra.Game.Games.Engines.Eclipse.DragonAge2.DragonAge2Engine;
using Andastra.Game.Games.Engines.Eclipse.Profiles;
using Andastra.Game.Games.Odyssey;
using Andastra.Game.Games.Odyssey.Profiles;

namespace Andastra.Game.Core
{
    /// <summary>
    /// Factory for creating engine instances based on BioWareGame enum.
    /// </summary>
    /// <remarks>
    /// Engine Factory:
    /// - Maps BioWareGame enum values to appropriate engine instances
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00404250 @ 0x00404250 determines game type and initializes appropriate engine
    /// - Original implementation: Game executable identifies itself and initializes engine accordingly
    /// - Cross-engine: All BioWare games follow similar pattern - determine game type, create engine, initialize
    /// - This factory provides unified access to all engine families (Odyssey, Aurora, Eclipse, Infinity)
    /// </remarks>
    public static class EngineFactory
    {
        /// <summary>
        /// Creates an engine instance for the specified BioWare game.
        /// </summary>
        /// <param name="bioWareGame">The BioWare game to create an engine for.</param>
        /// <returns>An initialized engine instance, or null if the game is not supported.</returns>
        /// <exception cref="NotSupportedException">Thrown when the game type is not supported or engine implementation is incomplete.</exception>
        public static IEngine CreateEngine(BioWareGame bioWareGame)
        {
            if (bioWareGame.IsOdyssey())
            {
                return CreateOdysseyEngine(bioWareGame);
            }
            else if (bioWareGame.IsEclipse())
            {
                return CreateEclipseEngine(bioWareGame);
            }
            else if (bioWareGame.IsAurora())
            {
                return CreateAuroraEngine(bioWareGame);
            }
            else if (bioWareGame.IsInfinity())
            {
                // Infinity Engine support is not yet implemented
                throw new NotSupportedException($"Infinity Engine games (e.g., {bioWareGame}) are not yet supported. Support for Baldur's Gate, Icewind Dale, and Planescape: Torment is planned but not yet implemented.");
            }
            else
            {
                throw new NotSupportedException($"Unknown or unsupported game type: {bioWareGame}");
            }
        }

        /// <summary>
        /// Gets the engine family for the specified BioWare game.
        /// </summary>
        public static Andastra.Game.Games.Common.EngineFamily GetEngineFamily(BioWareGame bioWareGame)
        {
            if (bioWareGame.IsOdyssey())
            {
                return Andastra.Game.Games.Common.EngineFamily.Odyssey;
            }
            else if (bioWareGame.IsEclipse())
            {
                return Andastra.Game.Games.Common.EngineFamily.Eclipse;
            }
            else if (bioWareGame.IsAurora())
            {
                return Andastra.Game.Games.Common.EngineFamily.Aurora;
            }
            else if (bioWareGame.IsInfinity())
            {
                // Infinity Engine support is not yet implemented
                return Andastra.Game.Games.Common.EngineFamily.Unknown;
            }
            else
            {
                return Andastra.Game.Games.Common.EngineFamily.Unknown;
            }
        }

        /// <summary>
        /// Checks if a BioWare game is currently supported.
        /// </summary>
        public static bool IsSupported(BioWareGame bioWareGame)
        {
            // Odyssey games are fully supported
            if (bioWareGame.IsOdyssey())
            {
                return true;
            }

            // Eclipse and Aurora games have base implementations but may not be fully functional
            // Return true to allow initialization, but actual gameplay may be limited
            if (bioWareGame.IsEclipse() || bioWareGame.IsAurora())
            {
                return true;
            }

            // Infinity Engine games are not yet implemented
            if (bioWareGame.IsInfinity())
            {
                return false;
            }

            return false;
        }

        /// <summary>
        /// Creates an Odyssey Engine instance.
        /// </summary>
        private static IEngine CreateOdysseyEngine(BioWareGame bioWareGame)
        {
            // Map BioWareGame to GameType for Odyssey profile factory
            GameType gameType;
            if (bioWareGame.IsK1())
            {
                gameType = GameType.K1;
            }
            else if (bioWareGame.IsK2())
            {
                gameType = GameType.K2;
            }
            else
            {
                throw new NotSupportedException($"Unsupported Odyssey game variant: {bioWareGame}");
            }

            // Create profile using Odyssey profile factory
            IEngineProfile profile = GameProfileFactory.CreateProfile(gameType);

            // Create and return Odyssey engine
            return new OdysseyEngine(profile);
        }

        /// <summary>
        /// Creates an Eclipse Engine instance.
        /// </summary>
        private static IEngine CreateEclipseEngine(BioWareGame bioWareGame)
        {
            // Map BioWareGame to GameType for Eclipse profile factory
            GameType gameType;
            if (bioWareGame.IsDragonAgeOrigins())
            {
                gameType = GameType.DA_ORIGINS;
            }
            else if (bioWareGame.IsDragonAge2())
            {
                gameType = GameType.DA2;
            }
            else
            {
                throw new NotSupportedException($"Unsupported Eclipse game variant: {bioWareGame}");
            }

            // Create profile using Eclipse profile factory
            IEngineProfile profile = EclipseProfileFactory.CreateProfile(gameType);

            // Create and return appropriate Eclipse engine instance
            if (gameType == GameType.DA_ORIGINS)
            {
                return new DragonAgeOriginsEngine(profile);
            }
            else if (gameType == GameType.DA2)
            {
                return new DragonAge2Engine(profile);
            }
            else
            {
                throw new NotSupportedException($"Unsupported Eclipse game type: {gameType}");
            }
        }

        /// <summary>
        /// Creates an Aurora Engine instance.
        /// </summary>
        private static IEngine CreateAuroraEngine(BioWareGame bioWareGame)
        {
            // Map BioWareGame to GameType for Aurora profile factory
            GameType gameType;
            if (bioWareGame.IsNWN1())
            {
                gameType = GameType.NWN;
            }
            else if (bioWareGame.IsNWN2())
            {
                gameType = GameType.NWN2;
            }
            else
            {
                throw new NotSupportedException($"Unsupported Aurora game variant: {bioWareGame}");
            }

            // Create profile using Aurora profile factory
            IEngineProfile profile = AuroraProfileFactory.CreateProfile(gameType);

            // Create and return Aurora engine
            return new AuroraEngine(profile);
        }
    }
}


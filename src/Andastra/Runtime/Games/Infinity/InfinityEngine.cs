using System;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Content.ResourceProviders;
using Andastra.Runtime.Engines.Common;

namespace Andastra.Runtime.Engines.Infinity
{
    /// <summary>
    /// Infinity Engine implementation for Baldur's Gate, Icewind Dale, and Planescape: Torment.
    /// </summary>
    /// <remarks>
    /// Infinity Engine:
    /// - Based on Infinity Engine architecture (Baldur's Gate, Icewind Dale, Planescape: Torment)
    /// - Resource provider: Uses Infinity-specific resource system (BIF files, KEY index files, override directory)
    /// - Game session: Coordinates module loading, entity management, script execution for Infinity games
    /// - Cross-engine: Similar engine initialization pattern to Odyssey/Aurora/Eclipse but different resource system
    ///   - Odyssey: RIM/ERF/BIF files with chitin.key index
    ///   - Aurora: HAK files with module files
    ///   - Eclipse: PCC/UPK packages with streaming resources
    ///   - Infinity: BIF files with KEY index files
    /// - Inheritance: BaseEngine (Runtime.Games.Common) implements common engine initialization
    ///   - Infinity: InfinityEngine : BaseEngine (Runtime.Games.Infinity) - Infinity-specific resource provider (InfinityResourceProvider)
    /// - Original implementation: Infinity Engine uses CResMan/CResManager for resource loading
    /// - Resource precedence: OVERRIDE > MODULE > BIF (via KEY) > HARDCODED
    /// - TODO: Reverse engineer specific function addresses from Infinity Engine executables using Ghidra MCP
    ///   - Baldur's Gate: BaldurGate.exe engine initialization functions
    ///   - Icewind Dale: IcewindDale.exe engine initialization functions
    ///   - Planescape: Torment: PlanescapeTorment.exe engine initialization functions
    /// </remarks>
    public class InfinityEngine : BaseEngine
    {
        private string _installationPath;

        public InfinityEngine(IEngineProfile profile)
            : base(profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            if (profile.EngineFamily != EngineFamily.Infinity)
            {
                throw new ArgumentException("Profile must be for Infinity engine family", nameof(profile));
            }
        }

        public override IEngineGame CreateGameSession()
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("Engine must be initialized before creating game session");
            }

            return new InfinityGameSession(this);
        }

        protected override IGameResourceProvider CreateResourceProvider(string installationPath)
        {
            if (string.IsNullOrEmpty(installationPath))
            {
                throw new ArgumentException("Installation path cannot be null or empty", nameof(installationPath));
            }

            _installationPath = installationPath;

            // Determine game type from installation path
            // Infinity Engine games: Baldur's Gate, Icewind Dale, Planescape: Torment
            // For now, use Unknown - can be extended to detect specific game
            GameType gameType = GameType.Unknown;

            // TODO: Detect specific Infinity Engine game type from installation path
            // Check for game-specific executables or files to determine game type
            // Baldur's Gate: BaldurGate.exe
            // Icewind Dale: IcewindDale.exe
            // Planescape: Torment: PlanescapeTorment.exe

            return new InfinityResourceProvider(installationPath, gameType);
        }
    }
}



using System;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Content.ResourceProviders;
using Andastra.Runtime.Engines.Common;

namespace Andastra.Runtime.Engines.Aurora
{
    /// <summary>
    /// Aurora Engine implementation for Neverwinter Nights and Neverwinter Nights 2.
    /// </summary>
    /// <remarks>
    /// Aurora Engine:
    /// - Based on Aurora Engine architecture (Neverwinter Nights, Neverwinter Nights 2)
    /// - Engine initialization: Similar pattern to Odyssey engine but for Aurora-based games
    /// - Resource provider: Uses Aurora-specific resource system (HAK files, module files, override directory)
    /// - Game session: Coordinates module loading, entity management, script execution for Aurora games
    /// - Cross-engine: Similar engine initialization pattern to Odyssey/Eclipse/Infinity but different resource system
    ///   - Odyssey: RIM/ERF/BIF files with chitin.key index
    ///   - Aurora: HAK files with module files and override directory
    ///   - Eclipse: PCC/UPK packages with RIM files and streaming resources
    ///   - Infinity: BIF files with KEY index files
    /// - Inheritance: BaseEngine (Runtime.Games.Common) implements common engine initialization
    ///   - Aurora: AuroraEngine : BaseEngine (Runtime.Games.Aurora) - Aurora-specific resource provider (AuroraResourceProvider)
    /// - Original implementation: Aurora Engine uses CExoResMan for resource loading
    /// - Resource precedence: OVERRIDE > MODULE > HAK (in load order) > BASE_GAME > HARDCODED
    /// - nwmain.exe: "MODULES=" @ 0x140d80d20, "OVERRIDE=" @ 0x140d80d50 (resource directory configuration)
    /// - nwmain.exe: LoadModule @ 0x140565c50, UnloadModule @ 0x14056df00 (module loading functions)
    /// - TODO: Reverse engineer specific function addresses from Aurora Engine executables using Ghidra MCP
    ///   - Neverwinter Nights: nwmain.exe engine initialization and resource loading functions
    ///   - Neverwinter Nights 2: nwn2main.exe engine initialization and resource loading functions
    /// </remarks>
    public class AuroraEngine : BaseEngine
    {
        private string _installationPath;

        public AuroraEngine(IEngineProfile profile)
            : base(profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            if (profile.EngineFamily != EngineFamily.Aurora)
            {
                throw new ArgumentException("Profile must be for Aurora engine family", nameof(profile));
            }
        }

        public override IEngineGame CreateGameSession()
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("Engine must be initialized before creating game session");
            }

            // TODO: STUB - Implement AuroraGameSession
            throw new NotImplementedException("Aurora game session not yet implemented");
        }

        protected override IGameResourceProvider CreateResourceProvider(string installationPath)
        {
            if (string.IsNullOrEmpty(installationPath))
            {
                throw new ArgumentException("Installation path cannot be null or empty", nameof(installationPath));
            }

            _installationPath = installationPath;

            // Determine game type from installation path
            // Aurora Engine games: Neverwinter Nights, Neverwinter Nights 2
            GameType gameType = DetectAuroraGameType(installationPath);

            return new AuroraResourceProvider(installationPath, gameType);
        }

        /// <summary>
        /// Detects the specific Aurora Engine game type from the installation path.
        /// </summary>
        /// <param name="installationPath">The installation path to check.</param>
        /// <returns>The detected game type, or Unknown if detection fails.</returns>
        /// <remarks>
        /// Aurora Engine Game Detection:
        /// - Based on Aurora Engine game detection patterns (Neverwinter Nights, Neverwinter Nights 2)
        /// - Detection method: Checks for game-specific executable files in installation directory
        /// - Neverwinter Nights: Checks for "nwmain.exe" or "NWMAIN.EXE"
        /// - Neverwinter Nights 2: Checks for "nwn2main.exe" or "NWN2MAIN.EXE"
        /// - Fallback: Checks for module directory structure and HAK files
        /// - Similar to Odyssey Engine detection pattern (swkotor.exe/swkotor2.exe detection)
        /// - Original implementation: Aurora Engine executables identify themselves via executable name
        /// - Cross-engine: Similar detection pattern across all BioWare engines (executable name + fallback file checks)
        /// </remarks>
        private static GameType DetectAuroraGameType(string installationPath)
        {
            if (string.IsNullOrEmpty(installationPath) || !System.IO.Directory.Exists(installationPath))
            {
                return GameType.Unknown;
            }

            // Check for Neverwinter Nights executable
            string nwnExe = System.IO.Path.Combine(installationPath, "nwmain.exe");
            string nwnExeUpper = System.IO.Path.Combine(installationPath, "NWMAIN.EXE");
            if (System.IO.File.Exists(nwnExe) || System.IO.File.Exists(nwnExeUpper))
            {
                // GameType enum doesn't have NWN/NWN2 yet, return Unknown for now
                // TODO: Extend GameType enum to support Aurora Engine games
                return GameType.Unknown;
            }

            // Check for Neverwinter Nights 2 executable
            string nwn2Exe = System.IO.Path.Combine(installationPath, "nwn2main.exe");
            string nwn2ExeUpper = System.IO.Path.Combine(installationPath, "NWN2MAIN.EXE");
            if (System.IO.File.Exists(nwn2Exe) || System.IO.File.Exists(nwn2ExeUpper))
            {
                // GameType enum doesn't have NWN/NWN2 yet, return Unknown for now
                // TODO: Extend GameType enum to support Aurora Engine games
                return GameType.Unknown;
            }

            // Fallback: Check for module directory and HAK files (indicates Aurora Engine installation)
            string modulesPath = System.IO.Path.Combine(installationPath, "modules");
            string hakPath = System.IO.Path.Combine(installationPath, "hak");
            if (System.IO.Directory.Exists(modulesPath) || System.IO.Directory.Exists(hakPath))
            {
                // Aurora Engine installation detected via directory structure
                return GameType.Unknown; // GameType enum doesn't support Aurora games yet
            }

            return GameType.Unknown;
        }
    }
}



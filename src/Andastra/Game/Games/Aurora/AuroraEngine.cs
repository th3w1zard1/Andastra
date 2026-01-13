using System;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Content.ResourceProviders;
using Andastra.Runtime.Core.Entities;
using Andastra.Game.Games.Common;
using Andastra.Game.Games.Common;
using Andastra.Game.Games.Aurora;

namespace Andastra.Game.Games.Aurora
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
    ///
    /// Engine Initialization Functions (nwmain.exe) - REQUIRES source investigation:
    /// - Entry point: WinMain (nwmain.exe: Windows entry point, initializes engine, address needs Ghidra verification)
    ///   - Located via PE entry point analysis and WinMain function signature
    ///   - Initializes COM, creates mutex, loads configuration, initializes CServerExoApp
    /// - CServerExoApp::CServerExoApp (nwmain.exe: constructor, initializes server application, address needs Ghidra verification)
    ///   - Creates CServerExoApp instance, initializes subsystems, sets up resource manager
    /// - CServerExoApp::Initialize (nwmain.exe: main initialization function, address needs Ghidra verification)
    ///   - Initializes CExoResMan, sets up resource paths, loads configuration, initializes game systems
    /// - CServerExoApp::GetResMan (nwmain.exe: gets CExoResMan instance, address needs Ghidra verification)
    ///   - Returns pointer to CExoResMan resource manager singleton
    /// - CExoResMan::CExoResMan (nwmain.exe: constructor, initializes resource manager, address needs Ghidra verification)
    ///   - Creates resource manager instance, initializes resource tables, sets up directory aliases
    /// - CExoResMan::Initialize (nwmain.exe: initializes resource manager with paths, address needs Ghidra verification)
    ///   - Sets up MODULES, OVERRIDE, HAK directory aliases, loads resource index files
    /// - CExoResMan::AddDir (nwmain.exe: adds directory alias to resource search path, address needs Ghidra verification)
    ///   - Registers directory alias (MODULES, OVERRIDE, etc.) for resource lookup precedence
    /// - Resource path configuration strings: "MODULES=" @ 0x140d80d20, "OVERRIDE=" @ 0x140d80d50 (nwmain.exe: VERIFIED)
    ///   - Directory name strings: "modules" @ 0x140d80f38, "override" @ 0x140d80f40 (nwmain.exe: VERIFIED)
    ///
    /// Module Loading Functions (nwmain.exe):
    /// - CServerExoApp::LoadModule @ 0x140565c50 (nwmain.exe: loads module by name, VERIFIED)
    ///   - Validates module name, creates CNWSModule instance, loads Module.ifo, initializes module state
    /// - CServerExoApp::UnloadModule @ 0x14056df00 (nwmain.exe: unloads current module, VERIFIED)
    ///   - Cleans up module resources, unloads areas, clears entity lists, resets module state
    /// - CNWSModule::LoadModule (nwmain.exe: module-specific loading implementation, address needs Ghidra verification)
    ///   - Loads Module.ifo, HAK files, entry area, GIT files, spawns entities, initializes scripting
    /// - CNWSModule::UnloadModule (nwmain.exe: module-specific unloading implementation, address needs Ghidra verification)
    ///   - Unloads areas, clears entities, frees resources, resets module flags
    ///
    /// Resource Loading Functions (nwmain.exe) - REQUIRES source investigation:
    /// - CExoResMan::Exists (nwmain.exe: checks if resource exists, address needs Ghidra verification)
    ///   - Searches resource in precedence order: OVERRIDE > MODULE > HAK > BASE_GAME > HARDCODED
    /// - CExoResMan::Get (nwmain.exe: loads resource data, address needs Ghidra verification)
    ///   - Loads resource bytes from highest precedence location, caches loaded resources
    /// - CExoResMan::GetResObject (nwmain.exe: gets resource object with metadata, address needs Ghidra verification)
    ///   - Returns CExoRes structure with resource data, size, type, and location information
    /// - CExoResMan::Demand (nwmain.exe: demands resource, adds to active resource list, address needs Ghidra verification)
    ///   - Marks resource as actively used, prevents garbage collection, tracks resource references
    /// - CExoResMan::Release (nwmain.exe: releases resource, allows garbage collection, address needs Ghidra verification)
    ///   - Decrements resource reference count, frees resource when count reaches zero
    /// - CExoResMan::SetResObject (nwmain.exe: sets/reserves resource in memory, address needs Ghidra verification)
    ///   - Registers resource in resource manager, used for resource injection and override
    /// - HAK file loading: CExoResMan::LoadHAK (nwmain.exe: loads HAK archive file, address needs Ghidra verification)
    ///   - Loads HAK (Hak Archive) ERF file, indexes resources, adds to resource search path
    /// - Module resource loading: CExoResMan::SetModuleRes (nwmain.exe: sets module context, address needs Ghidra verification)
    ///   - Sets current module for module-specific resource lookups, enables module override directory
    ///
    /// Engine Initialization Functions (nwn2main.exe) - REQUIRES source investigation:
    /// - Entry point: WinMain (nwn2main.exe: Windows entry point, address needs Ghidra verification)
    ///   - Similar initialization pattern to nwmain.exe but with NWN2-specific modifications
    /// - CServerExoApp::CServerExoApp (nwn2main.exe: constructor, address needs Ghidra verification)
    ///   - NWN2-specific initialization, may have different structure offsets or additional subsystems
    /// - CServerExoApp::Initialize (nwn2main.exe: main initialization, address needs Ghidra verification)
    ///   - NWN2-specific resource paths and configuration loading
    /// - CExoResMan initialization functions (nwn2main.exe: addresses need Ghidra verification)
    ///   - Similar function signatures to nwmain.exe but addresses will differ
    ///   - May have NWN2-specific resource loading optimizations or features
    ///
    /// Module Loading Functions (nwn2main.exe) - REQUIRES source investigation:
    /// - CServerExoApp::LoadModule (nwn2main.exe: address needs Ghidra verification)
    ///   - NWN2-specific module loading with enhanced features or different file formats
    /// - CServerExoApp::UnloadModule (nwn2main.exe: address needs Ghidra verification)
    ///   - NWN2-specific module unloading with enhanced cleanup
    /// - CNWSModule functions (nwn2main.exe: addresses need Ghidra verification)
    ///   - Similar patterns to nwmain.exe but may have NWN2-specific enhancements
    ///
    /// Cross-Engine Comparison:
    /// - Odyssey (swkotor.exe/swkotor2.exe): 0x00404250 @ 0x00404250 (WinMain equivalent, VERIFIED)
    ///   - Similar initialization pattern: Entry point -> Engine initialization -> Resource provider -> Module loading
    /// - Aurora (nwmain.exe/nwn2main.exe): Uses CExoResMan instead of CExoKeyTable (similar pattern, different resource system)
    ///   - HAK file support distinguishes Aurora from Odyssey resource system
    /// - Common patterns: All engines initialize resource manager, set up directory aliases, load configuration
    ///
    /// Inheritance Structure:
    /// - BaseEngine (Runtime.Games.Common) - Common engine initialization (Initialize, Shutdown, CreateGameSession)
    ///   - AuroraEngine : BaseEngine (Runtime.Games.Aurora) - Aurora-specific (nwmain.exe, nwn2main.exe)
    ///     - Uses CExoResMan for resource management (different from Odyssey's CExoKeyTable)
    ///     - HAK file support for module resources
    ///     - Module.ifo-based module system (different from Odyssey's IFO format)
    ///
    /// NOTE: Function addresses marked as VERIFIED have been confirmed via existing codebase documentation.
    /// Function addresses marked as "needs Ghidra verification" require further analysis to obtain exact addresses.
    /// All addresses follow standard x64 PE format with base address 0x140000000 (typical for Windows executables).
    /// This documentation comprehensively outlines all engine initialization and resource loading functions that need to be reverse engineered
    /// for complete 1:1 parity with the original Aurora Engine executables.
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

            return new AuroraGameSession(this);
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
            // Based on nwmain.exe: SDL_main @ 0x140046340 calls RegisterCrashHandler("nwmain") to identify game
            // Game detection: Executable name "nwmain.exe" is the primary identifier for Neverwinter Nights 1
            string nwnExe = System.IO.Path.Combine(installationPath, "nwmain.exe");
            string nwnExeUpper = System.IO.Path.Combine(installationPath, "NWMAIN.EXE");
            if (System.IO.File.Exists(nwnExe) || System.IO.File.Exists(nwnExeUpper))
            {
                return GameType.NWN;
            }

            // Check for Neverwinter Nights 2 executable
            // Game detection: Executable name "nwn2main.exe" is the primary identifier for Neverwinter Nights 2
            string nwn2Exe = System.IO.Path.Combine(installationPath, "nwn2main.exe");
            string nwn2ExeUpper = System.IO.Path.Combine(installationPath, "NWN2MAIN.EXE");
            if (System.IO.File.Exists(nwn2Exe) || System.IO.File.Exists(nwn2ExeUpper))
            {
                return GameType.NWN2;
            }

            // Fallback: Check for module directory and HAK files (indicates Aurora Engine installation)
            // Based on nwmain.exe: CExoResMan::Initialize sets up MODULES and HAK directory aliases
            // Resource path configuration strings: "MODULES=" @ 0x140d80d20, "OVERRIDE=" @ 0x140d80d50 (nwmain.exe: VERIFIED)
            // Directory name strings: "modules" @ 0x140d80f38, "override" @ 0x140d80f40 (nwmain.exe: VERIFIED)
            string modulesPath = System.IO.Path.Combine(installationPath, "modules");
            string hakPath = System.IO.Path.Combine(installationPath, "hak");
            if (System.IO.Directory.Exists(modulesPath) || System.IO.Directory.Exists(hakPath))
            {
                // Aurora Engine installation detected via directory structure
                // Default to NWN if we can't determine which specific game
                return GameType.NWN;
            }

            return GameType.Unknown;
        }

        protected override World CreateWorld()
        {
            var timeManager = new AuroraTimeManager();
            return new World(timeManager);
        }
    }
}



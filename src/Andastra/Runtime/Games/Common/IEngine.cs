using System;
using System.Collections.Generic;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Game.Scripting.Interfaces;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Common
{
    /// <summary>
    /// Engine family enumeration for grouping related engines.
    /// </summary>
    public enum EngineFamily
    {
        /// <summary>
        /// Aurora Engine (NWN, NWN2)
        /// </summary>
        Aurora,

        /// <summary>
        /// Odyssey Engine (KOTOR, KOTOR2, Jade Empire)
        /// </summary>
        Odyssey,

        /// <summary>
        /// Eclipse Engine (Dragon Age Origins, Dragon Age 2)
        /// </summary>
        Eclipse,

        /// <summary>
        /// Unknown or unsupported engine
        /// </summary>
        Unknown
    }

    /// <summary>
    /// Base interface for all BioWare engine implementations.
    /// </summary>
    /// <remarks>
    /// Engine Interface - Common Contract Across All BioWare Engines:
    ///
    /// This interface defines the common contract shared across all BioWare engine families:
    /// - Odyssey Engine (swkotor.exe, swkotor2.exe): KOTOR 1/2, Jade Empire
    /// - Aurora Engine (nwmain.exe, nwn2main.exe): Neverwinter Nights, Neverwinter Nights 2
    /// - Eclipse Engine (daorigins.exe, DragonAge2.exe): Dragon Age: Origins, Dragon Age 2
    ///
    /// Common Interface Patterns (Identified via Cross-Engine Reverse Engineering):
    ///
    /// 1. Engine Lifecycle Pattern (Common to All Engines):
    ///    - Constructor: Validates IEngineProfile, stores profile reference
    ///      * All engines validate profile is not null
    ///      * All engines validate profile.EngineFamily matches expected engine family
    ///      * Pattern: BaseEngine constructor stores _profile, subclasses validate engine family
    ///    - Initialize(installationPath): Sets up engine resources and systems
    ///      * All engines: Validate installationPath is not null/empty
    ///      * All engines: Create resource provider via CreateResourceProvider(installationPath)
    ///      * All engines: Create world via CreateWorld() (default: new World())
    ///      * All engines: Create engine API via _profile.CreateEngineApi()
    ///      * All engines: Set _initialized = true after successful initialization
    ///      * All engines: Throw InvalidOperationException if already initialized
    ///    - CreateGameSession(): Creates game session for gameplay
    ///      * All engines: Check _initialized, throw InvalidOperationException if not initialized
    ///      * All engines: Return IEngineGame instance (engine-specific implementation)
    ///    - Shutdown(): Cleans up engine resources
    ///      * All engines: Clear world reference (set to null)
    ///      * All engines: Clear resource provider reference (set to null)
    ///      * All engines: Clear engine API reference (set to null)
    ///      * All engines: Set _initialized = false
    ///      * All engines: Safe to call multiple times (idempotent)
    ///
    /// 2. Common Properties (Read-Only, Set During Initialization):
    ///    - EngineFamily: Engine family enumeration (Odyssey, Aurora, Eclipse)
    ///      * Source: _profile.EngineFamily (delegated to profile)
    ///      * Pattern: All engines delegate to profile, no engine-specific logic
    ///    - Profile: Game profile for this engine instance
    ///      * Source: Stored in constructor, never changes
    ///      * Pattern: All engines store profile in constructor, provide read-only access
    ///    - ResourceProvider: Resource loading provider for game resources
    ///      * Source: Created in Initialize() via CreateResourceProvider()
    ///      * Pattern: All engines create engine-specific resource provider (Odyssey: GameResourceProvider, Aurora: AuroraResourceProvider, Eclipse: EclipseResourceProvider)
    ///    - World: World instance for entity and area management
    ///      * Source: Created in Initialize() via CreateWorld() (default: new World())
    ///      * Pattern: All engines use same World class from Runtime.Core.Entities
    ///    - EngineApi: Engine API for script function implementations
    ///      * Source: Created in Initialize() via _profile.CreateEngineApi()
    ///      * Pattern: All engines delegate engine API creation to profile
    ///
    /// 3. Common Method Patterns:
    ///    - Initialize(string installationPath):
    ///      * Signature: void Initialize(string installationPath)
    ///      * Validation: All engines validate installationPath is not null/empty
    ///      * Initialization sequence: ResourceProvider -> World -> EngineApi -> _initialized = true
    ///      * Error handling: All engines throw ArgumentException for invalid path, InvalidOperationException for already initialized
    ///    - Shutdown():
    ///      * Signature: void Shutdown()
    ///      * Cleanup sequence: World -> ResourceProvider -> EngineApi -> _initialized = false
    ///      * Idempotent: All engines safely handle multiple shutdown calls
    ///    - CreateGameSession():
    ///      * Signature: IEngineGame CreateGameSession()
    ///      * Validation: All engines check _initialized before creating session
    ///      * Return: Engine-specific IEngineGame implementation (OdysseyGameSession, AuroraGameSession, EclipseGameSession)
    ///      * Error handling: All engines throw InvalidOperationException if not initialized
    ///
    /// 4. Engine-Specific Differences (NOT Part of Common Interface):
    ///    - Resource Provider Creation:
    ///      * Odyssey: Creates GameResourceProvider wrapping Installation object
    ///      * Aurora: Creates AuroraResourceProvider with game type detection
    ///      * Eclipse: Creates EclipseResourceProvider with game type detection
    ///      * Pattern: All use abstract CreateResourceProvider() method implemented in subclasses
    ///    - Game Session Creation:
    ///      * Odyssey: Returns OdysseyGameSession instance
    ///      * Aurora: Returns AuroraGameSession instance
    ///      * Eclipse: Returns EclipseGameSession instance (abstract, game-specific subclasses)
    ///      * Pattern: All use abstract CreateGameSession() method implemented in subclasses
    ///    - Constructor Parameters:
    ///      * Odyssey/Aurora: Constructor takes IEngineProfile only
    ///      * Eclipse: Constructor takes IEngineProfile + Game enum (additional parameter)
    ///      * Pattern: BaseEngine takes profile, Eclipse adds game parameter for game type detection
    ///
    /// 5. Cross-Engine Reverse Engineering References:
    ///    - Odyssey Engine (swkotor.exe, swkotor2.exe):
    ///      * 0x00404250 @ 0x00404250 (swkotor2.exe: WinMain equivalent, engine initialization)
    ///      * 0x00633270 @ 0x00633270 (swkotor2.exe: Sets up resource directories)
    ///      * Initialization pattern: Entry point -> Resource setup -> Module loading
    ///    - Aurora Engine (nwmain.exe, nwn2main.exe):
    ///      * CServerExoApp::Initialize (nwmain.exe: main initialization function)
    ///      * CExoResMan::Initialize (nwmain.exe: resource manager initialization)
    ///      * Initialization pattern: Entry point -> CServerExoApp -> CExoResMan -> Module loading
    ///    - Eclipse Engine (daorigins.exe, DragonAge2.exe):
    ///      * UnrealScript-based: Uses message passing system instead of direct function calls
    ///      * LoadModuleMessage @ 0x00b17da4 (daorigins.exe: module loading message)
    ///      * Initialization pattern: Entry point -> Game initialization -> Module message handling
    ///
    /// 6. Implementation Requirements:
    ///    - All engine implementations MUST inherit from BaseEngine abstract class
    ///    - All engine implementations MUST implement abstract CreateResourceProvider() method
    ///    - All engine implementations MUST implement abstract CreateGameSession() method
    ///    - All engine implementations MUST validate profile.EngineFamily matches expected family
    ///    - All engine implementations MUST follow common initialization/shutdown lifecycle
    ///
    /// 7. Usage Pattern:
    ///    ```csharp
    ///    // Create engine with profile
    ///    IEngineProfile profile = GameProfileFactory.CreateProfile(GameType.K1);
    ///    IEngine engine = new OdysseyEngine(profile);
    ///
    ///    // Initialize engine
    ///    engine.Initialize("C:\\Games\\KOTOR");
    ///
    ///    // Create game session
    ///    IEngineGame gameSession = engine.CreateGameSession();
    ///
    ///    // Use game session
    ///    await gameSession.LoadModuleAsync("end_m01aa");
    ///
    ///    // Shutdown engine
    ///    engine.Shutdown();
    ///    ```
    ///
    /// This interface ensures consistent engine behavior across all BioWare engine families while
    /// allowing engine-specific implementations through the BaseEngine abstract class pattern.
    /// </remarks>
    public interface IEngine
    {
        /// <summary>
        /// Gets the engine family (Odyssey, Aurora, Eclipse).
        /// </summary>
        EngineFamily EngineFamily { get; }

        /// <summary>
        /// Gets the game profile for this engine instance.
        /// </summary>
        IEngineProfile Profile { get; }

        /// <summary>
        /// Gets the resource provider for loading game resources.
        /// </summary>
        IGameResourceProvider ResourceProvider { get; }

        /// <summary>
        /// Gets the world instance.
        /// </summary>
        IWorld World { get; }

        /// <summary>
        /// Gets the engine API instance.
        /// </summary>
        IEngineApi EngineApi { get; }

        /// <summary>
        /// Creates a new game session for this engine.
        /// </summary>
        IEngineGame CreateGameSession();

        /// <summary>
        /// Initializes the engine with the specified installation path.
        /// </summary>
        void Initialize(string installationPath);

        /// <summary>
        /// Shuts down the engine and cleans up resources.
        /// </summary>
        void Shutdown();
    }

}



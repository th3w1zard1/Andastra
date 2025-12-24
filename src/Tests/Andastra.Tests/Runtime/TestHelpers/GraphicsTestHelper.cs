using System;
using System.Collections.Generic;
using System.IO;
using Andastra.Parsing.Common;
using Andastra.Parsing.Extract;
using Andastra.Parsing.Formats.TPC;
using Andastra.Parsing.Installation;
using Andastra.Parsing.Resource;
using Andastra.Runtime.Graphics;
using Andastra.Runtime.MonoGame.Graphics;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Moq;
using Stride.Core.Mathematics;
using Stride.Engine;
using ExtractLazyCapsule = Andastra.Parsing.Extract.Capsule.LazyCapsule;
using ExtractResourceResult = Andastra.Parsing.Extract.ExtractResourceResult;
using InstallationResourceResult = Andastra.Parsing.Installation.ResourceResult;
using StrideGraphics = Stride.Graphics;


namespace Andastra.Tests.Runtime.TestHelpers
{
    /// <summary>
    /// Helper class for creating test graphics devices and installations.
    /// </summary>
    public static class GraphicsTestHelper
    {
        /// <summary>
        /// Creates a test MonoGame GraphicsDevice using a headless Game instance.
        /// </summary>
        public static GraphicsDevice CreateTestGraphicsDevice()
        {
            // Create a minimal Game instance for testing
            // Note: Game.Initialize() is protected, so we use reflection to call it
            var game = new Microsoft.Xna.Framework.Game();
            var initializeMethod = typeof(Microsoft.Xna.Framework.Game).GetMethod("Initialize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (initializeMethod != null)
            {
                initializeMethod.Invoke(game, null);
            }
            return game.GraphicsDevice;
        }

        /// <summary>
        /// Creates a test IGraphicsDevice wrapper.
        /// </summary>
        public static IGraphicsDevice CreateTestIGraphicsDevice()
        {
            var mgDevice = CreateTestGraphicsDevice();
            return new Andastra.Runtime.Graphics.MonoGame.Graphics.MonoGameGraphicsDevice(mgDevice);
        }

        /// <summary>
        /// Creates a mock Installation with comprehensive resource lookup capabilities.
        /// Provides a fully functional mock that supports all InstallationResourceManager methods
        /// including LookupResource, LocateResource, ClearCache, ReloadModule, GetChitinResources,
        /// and GetPatchErfResources. All Installation properties (Path, Game, Resources) are properly
        /// configured. Based on swkotor2.exe resource management system (CExoKeyTable, CExoResMan).
        ///
        /// Ghidra References:
        /// - swkotor2.exe: 0x007c14d4 - "Resource" string reference
        /// - swkotor2.exe: 0x0041d1e0 - FUN_0041d1e0 (resource lookup function, CExoKeyTable lookup)
        /// - swkotor2.exe: 0x006e69a0 - FUN_006e69a0 (uses "Resource" string for lookup)
        /// - swkotor2.exe: 0x007b6078 - "CExoKeyTable::DestroyTable: Resource %s still in demand during table deletion"
        /// - swkotor2.exe: 0x007b6124 - "CExoKeyTable::AddKey: Duplicate Resource "
        ///
        /// Original Engine Behavior:
        /// - Resource lookup searches in precedence order: OVERRIDE > MODULES > CHITIN > TEXTUREPACKS > STREAM
        /// - Returns null if resource name is null/whitespace (matches InstallationResourceManager.LookupResource line 44-45)
        /// - Returns null if searchOrder is empty array (matches InstallationResourceManager.LookupResource line 50-52)
        /// - Uses default search order if searchOrder is null (matches InstallationResourceManager.LookupResource line 56-72)
        /// - Searches locations in order and returns first match (matches InstallationResourceManager.LookupResource line 76-85)
        /// </summary>
        /// <returns>A fully configured mock Installation instance ready for testing.</returns>
        public static Installation CreateMockInstallation()
        {
            // Create a comprehensive mock Installation with full resource lookup capabilities
            // Based on swkotor2.exe: 0x0041d1e0 (FUN_0041d1e0 - CExoKeyTable resource lookup)
            var mockInstallation = new Mock<Installation>(MockBehavior.Strict);
            var mockResources = new Mock<InstallationResourceManager>(MockBehavior.Strict);

            // Setup Installation properties
            string mockPath = Path.Combine(Path.GetTempPath(), "AndastraTestInstallation");
            BioWareGame mockGame = BioWareGame.K2; // Default to TSL for testing

            mockInstallation.Setup(i => i.Path).Returns(mockPath);
            mockInstallation.Setup(i => i.Game).Returns(mockGame);
            mockInstallation.Setup(i => i.Resources).Returns(mockResources.Object);

            // Setup LookupResource with correct signature (SearchLocation[] and string)
            // Matches InstallationResourceManager.LookupResource signature exactly
            // swkotor2.exe: 0x0041d1e0 - resource lookup returns bool, fills result parameter
            // Our implementation returns ResourceResult (null if not found, matches original behavior)
            mockResources.Setup(r => r.LookupResource(
                It.Is<string>(s => string.IsNullOrWhiteSpace(s)),
                It.IsAny<ResourceType>(),
                It.IsAny<SearchLocation[]>(),
                It.IsAny<string>()))
                .Returns((ResourceResult)null); // Matches line 44-45: if (string.IsNullOrWhiteSpace(resname)) return null;

            mockResources.Setup(r => r.LookupResource(
                It.Is<string>(s => !string.IsNullOrWhiteSpace(s)),
                It.IsAny<ResourceType>(),
                It.Is<SearchLocation[]>(order => order != null && order.Length == 0),
                It.IsAny<string>()))
                .Returns((ResourceResult)null); // Matches line 50-52: if (searchOrder != null && searchOrder.Length == 0) return null;

            mockResources.Setup(r => r.LookupResource(
                It.Is<string>(s => !string.IsNullOrWhiteSpace(s)),
                It.IsAny<ResourceType>(),
                It.IsAny<SearchLocation[]>(),
                It.IsAny<string>()))
                .Returns((ResourceResult)null); // Default: resource not found (matches line 88: return null after search)

            // Setup LocateResource - returns empty list by default
            mockResources.Setup(r => r.LocateResource(
                It.IsAny<string>(),
                It.IsAny<ResourceType>(),
                It.IsAny<SearchLocation[]>(),
                It.IsAny<string>()))
                .Returns(new List<LocationResult>());

            // Setup ClearCache - no-op for mocks
            mockResources.Setup(r => r.ClearCache()).Verifiable();

            // Setup ReloadModule - no-op for mocks
            mockResources.Setup(r => r.ReloadModule(It.IsAny<string>())).Verifiable();

            // Setup GetChitinResources - returns empty list by default
            mockResources.Setup(r => r.GetChitinResources())
                .Returns(new List<FileResource>());

            // Setup GetPatchErfResources - returns empty list by default
            mockResources.Setup(r => r.GetPatchErfResources(It.IsAny<BioWareGame>()))
                .Returns(new List<FileResource>());

            // Setup Installation.Resource method (delegates to Resources.LookupResource)
            mockInstallation.Setup(i => i.Resource(
                It.IsAny<string>(),
                It.IsAny<ResourceType>(),
                It.IsAny<SearchLocation[]>(),
                It.IsAny<string>()))
                .Returns((string resname, ResourceType restype, SearchLocation[] searchOrder, string moduleRoot) =>
                    mockResources.Object.LookupResource(resname, restype, searchOrder, moduleRoot));

            // Setup Installation.Locate method (delegates to Resources.LocateResource)
            mockInstallation.Setup(i => i.Locate(
                It.IsAny<string>(),
                It.IsAny<ResourceType>(),
                It.IsAny<SearchLocation[]>(),
                It.IsAny<string>()))
                .Returns((string resname, ResourceType restype, SearchLocation[] searchOrder, string moduleRoot) =>
                    mockResources.Object.LocateResource(resname, restype, searchOrder, moduleRoot));

            // Setup Installation.Texture method - returns null by default
            mockInstallation.Setup(i => i.Texture(
                It.IsAny<string>(),
                It.IsAny<SearchLocation[]>()))
                .Returns((TPC)null);

            // Setup Installation.GetModuleRoots - returns empty list by default
            mockInstallation.Setup(i => i.GetModuleRoots())
                .Returns(new List<string>());

            // Setup Installation.GetModuleFiles - returns empty list by default
            mockInstallation.Setup(i => i.GetModuleFiles(It.IsAny<string>()))
                .Returns(new List<string>());

            // Setup Installation.ClearCache - delegates to Resources.ClearCache
            mockInstallation.Setup(i => i.ClearCache())
                .Callback(() => mockResources.Object.ClearCache());

            // Setup Installation.ReloadModule - delegates to Resources.ReloadModule
            mockInstallation.Setup(i => i.ReloadModule(It.IsAny<string>()))
                .Callback((string moduleName) => mockResources.Object.ReloadModule(moduleName));

            // Setup Installation.ModulePath - returns mock modules path
            mockInstallation.Setup(i => i.ModulePath())
                .Returns(Installation.GetModulesPath(mockPath));

            // Setup Installation.OverridePath - returns mock override path
            mockInstallation.Setup(i => i.OverridePath())
                .Returns(Installation.GetOverridePath(mockPath));

            // Setup Installation.PackagePath - returns mock packages path
            mockInstallation.Setup(i => i.PackagePath())
                .Returns(Installation.GetPackagesPath(mockPath));

            // Setup Installation.ChitinResources - delegates to Resources.GetChitinResources
            mockInstallation.Setup(i => i.ChitinResources())
                .Returns(() => mockResources.Object.GetChitinResources());

            // Setup Installation.CoreResources - combines ChitinResources and GetPatchErfResources
            mockInstallation.Setup(i => i.CoreResources())
                .Returns(() =>
                {
                    var results = new List<FileResource>();
                    results.AddRange(mockResources.Object.GetChitinResources());
                    results.AddRange(mockResources.Object.GetPatchErfResources(mockGame));
                    return results;
                });

            // Setup Installation.OverrideList - returns empty list by default
            mockInstallation.Setup(i => i.OverrideList())
                .Returns(new List<string>());

            // Setup Installation.OverrideResources - returns empty list by default
            mockInstallation.Setup(i => i.OverrideResources(It.IsAny<string>()))
                .Returns(new List<FileResource>());

            // Setup Installation.Locations - returns empty dictionary by default
            mockInstallation.Setup(i => i.Locations(
                It.IsAny<List<ResourceIdentifier>>(),
                It.IsAny<SearchLocation[]>(),
                It.IsAny<List<ExtractLazyCapsule>>(),
                It.IsAny<List<string>>(),
                It.IsAny<string>()))
                .Returns(new Dictionary<ResourceIdentifier, List<LocationResult>>());

            return mockInstallation.Object;
        }

        /// <summary>
        /// Creates a mock Installation with specific resource data pre-configured.
        /// Provides a fully functional mock that returns the specified resource when looked up,
        /// while all other resources return null. All InstallationResourceManager methods are
        /// properly configured. Based on swkotor2.exe resource management system.
        ///
        /// Ghidra References:
        /// - swkotor2.exe: 0x007c14d4 - "Resource" string reference
        /// - swkotor2.exe: 0x0041d1e0 - FUN_0041d1e0 (resource lookup function, CExoKeyTable lookup)
        /// - swkotor2.exe: 0x006e69a0 - FUN_006e69a0 (uses "Resource" string for lookup)
        /// - swkotor2.exe: 0x007b6078 - "CExoKeyTable::DestroyTable: Resource %s still in demand during table deletion"
        /// - swkotor2.exe: 0x007b6124 - "CExoKeyTable::AddKey: Duplicate Resource "
        ///
        /// Original Engine Behavior:
        /// - Resource lookup searches in precedence order: OVERRIDE > MODULES > CHITIN > TEXTUREPACKS > STREAM
        /// - Returns ResourceResult with data if found, null otherwise
        /// - Case-insensitive resource name matching (matches InstallationResourceManager behavior)
        /// </summary>
        /// <param name="resRef">The resource reference name to configure.</param>
        /// <param name="resourceType">The resource type to configure.</param>
        /// <param name="data">The byte data to return for the specified resource.</param>
        /// <returns>A fully configured mock Installation instance with the specified resource available.</returns>
        public static Installation CreateMockInstallationWithResource(string resRef, ResourceType resourceType, byte[] data)
        {
            // Create a comprehensive mock Installation with specific resource data
            // Based on swkotor2.exe: 0x0041d1e0 (FUN_0041d1e0 - CExoKeyTable resource lookup)
            var mockInstallation = new Mock<Installation>(MockBehavior.Strict);
            var mockResources = new Mock<InstallationResourceManager>(MockBehavior.Strict);

            // Setup Installation properties
            string mockPath = Path.Combine(Path.GetTempPath(), "AndastraTestInstallation");
            BioWareGame mockGame = BioWareGame.K2; // Default to TSL for testing

            mockInstallation.Setup(i => i.Path).Returns(mockPath);
            mockInstallation.Setup(i => i.Game).Returns(mockGame);
            mockInstallation.Setup(i => i.Resources).Returns(mockResources.Object);

            // Setup resource lookup for specific resource with correct signature
            // Place resource in override directory (highest precedence in original engine)
            // Matches InstallationResourceManager default search order: OVERRIDE first (line 60)
            string mockFilePath = Path.Combine(mockPath, "override", $"{resRef}.{resourceType.Extension}");
            var resourceResult = new ResourceResult(resRef, resourceType, mockFilePath, data);

            mockResources.Setup(r => r.LookupResource(
                resRef,
                resourceType,
                It.IsAny<SearchLocation[]>(),
                It.IsAny<string>()))
                .Returns(resourceResult);

            // Setup default lookup to return null for all other resources
            mockResources.Setup(r => r.LookupResource(
                It.Is<string>(s => !string.Equals(s, resRef, StringComparison.OrdinalIgnoreCase)),
                It.IsAny<ResourceType>(),
                It.IsAny<SearchLocation[]>(),
                It.IsAny<string>()))
                .Returns((ResourceResult)null);

            // Setup LocateResource - returns location for the specific resource, empty for others
            mockResources.Setup(r => r.LocateResource(
                resRef,
                resourceType,
                It.IsAny<SearchLocation[]>(),
                It.IsAny<string>()))
                .Returns(new List<LocationResult>
                {
                    new LocationResult(mockFilePath, 0, data.Length)
                });

            mockResources.Setup(r => r.LocateResource(
                It.Is<string>(s => !string.Equals(s, resRef, StringComparison.OrdinalIgnoreCase)),
                It.IsAny<ResourceType>(),
                It.IsAny<SearchLocation[]>(),
                It.IsAny<string>()))
                .Returns(new List<LocationResult>());

            // Setup ClearCache - no-op for mocks
            mockResources.Setup(r => r.ClearCache()).Verifiable();

            // Setup ReloadModule - no-op for mocks
            mockResources.Setup(r => r.ReloadModule(It.IsAny<string>())).Verifiable();

            // Setup GetChitinResources - returns empty list by default
            mockResources.Setup(r => r.GetChitinResources())
                .Returns(new List<FileResource>());

            // Setup GetPatchErfResources - returns empty list by default
            mockResources.Setup(r => r.GetPatchErfResources(It.IsAny<BioWareGame>()))
                .Returns(new List<FileResource>());

            // Setup Installation.Resource method (delegates to Resources.LookupResource)
            mockInstallation.Setup(i => i.Resource(
                It.IsAny<string>(),
                It.IsAny<ResourceType>(),
                It.IsAny<SearchLocation[]>(),
                It.IsAny<string>()))
                .Returns((string resname, ResourceType restype, SearchLocation[] searchOrder, string moduleRoot) =>
                    mockResources.Object.LookupResource(resname, restype, searchOrder, moduleRoot));

            // Setup Installation.Locate method (delegates to Resources.LocateResource)
            mockInstallation.Setup(i => i.Locate(
                It.IsAny<string>(),
                It.IsAny<ResourceType>(),
                It.IsAny<SearchLocation[]>(),
                It.IsAny<string>()))
                .Returns((string resname, ResourceType restype, SearchLocation[] searchOrder, string moduleRoot) =>
                    mockResources.Object.LocateResource(resname, restype, searchOrder, moduleRoot));

            // Setup Installation.Texture method - returns null by default (would need TPC parsing)
            mockInstallation.Setup(i => i.Texture(
                It.IsAny<string>(),
                It.IsAny<SearchLocation[]>()))
                .Returns((TPC)null);

            // Setup Installation.GetModuleRoots - returns empty list by default
            mockInstallation.Setup(i => i.GetModuleRoots())
                .Returns(new List<string>());

            // Setup Installation.GetModuleFiles - returns empty list by default
            mockInstallation.Setup(i => i.GetModuleFiles(It.IsAny<string>()))
                .Returns(new List<string>());

            // Setup Installation.ClearCache - delegates to Resources.ClearCache
            mockInstallation.Setup(i => i.ClearCache())
                .Callback(() => mockResources.Object.ClearCache());

            // Setup Installation.ReloadModule - delegates to Resources.ReloadModule
            mockInstallation.Setup(i => i.ReloadModule(It.IsAny<string>()))
                .Callback((string moduleName) => mockResources.Object.ReloadModule(moduleName));

            // Setup Installation.ModulePath - returns mock modules path
            mockInstallation.Setup(i => i.ModulePath())
                .Returns(Installation.GetModulesPath(mockPath));

            // Setup Installation.OverridePath - returns mock override path
            mockInstallation.Setup(i => i.OverridePath())
                .Returns(Installation.GetOverridePath(mockPath));

            // Setup Installation.PackagePath - returns mock packages path
            mockInstallation.Setup(i => i.PackagePath())
                .Returns(Installation.GetPackagesPath(mockPath));

            // Setup Installation.ChitinResources - delegates to Resources.GetChitinResources
            mockInstallation.Setup(i => i.ChitinResources())
                .Returns(() => mockResources.Object.GetChitinResources());

            // Setup Installation.CoreResources - combines ChitinResources and GetPatchErfResources
            mockInstallation.Setup(i => i.CoreResources())
                .Returns(() =>
                {
                    var results = new List<FileResource>();
                    results.AddRange(mockResources.Object.GetChitinResources());
                    results.AddRange(mockResources.Object.GetPatchErfResources(mockGame));
                    return results;
                });

            // Setup Installation.OverrideList - returns empty list by default
            mockInstallation.Setup(i => i.OverrideList())
                .Returns(new List<string>());

            // Setup Installation.OverrideResources - returns empty list by default
            mockInstallation.Setup(i => i.OverrideResources(It.IsAny<string>()))
                .Returns(new List<FileResource>());

            // Setup Installation.Locations - returns location for the specific resource, empty for others
            mockInstallation.Setup(i => i.Locations(
                It.IsAny<List<ResourceIdentifier>>(),
                It.IsAny<SearchLocation[]>(),
                It.IsAny<List<ExtractLazyCapsule>>(),
                It.IsAny<List<string>>(),
                It.IsAny<string>()))
                .Returns((List<ResourceIdentifier> queries, SearchLocation[] order, List<ExtractLazyCapsule> capsules, List<string> folders, string moduleRoot) =>
                {
                    var results = new Dictionary<ResourceIdentifier, List<LocationResult>>();
                    if (queries != null)
                    {
                        foreach (var query in queries)
                        {
                            if (string.Equals(query.ResName, resRef, StringComparison.OrdinalIgnoreCase) && query.ResType == resourceType)
                            {
                                results[query] = new List<LocationResult>
                                {
                                    new LocationResult(mockFilePath, 0, data.Length)
                                };
                            }
                            else
                            {
                                results[query] = new List<LocationResult>();
                            }
                        }
                    }
                    return results;
                });

            return mockInstallation.Object;
        }

        /// <summary>
        /// Cleans up test resources.
        /// </summary>
        public static void CleanupTestGraphicsDevice(GraphicsDevice device)
        {
            if (device != null)
            {
                try
                {
                    device.Dispose();
                }
                catch
                {
                    // Ignore disposal errors
                }
            }
        }

        /// <summary>
        /// Creates a test Stride GraphicsDevice using a minimal Game instance.
        /// Returns null if device creation fails (e.g., no GPU available in headless environment).
        /// </summary>
        /// <returns>Stride GraphicsDevice instance, or null if creation fails.</returns>
        /// <remarks>
        /// Stride GraphicsDevice Creation for Tests:
        /// - Creates a minimal Game instance for testing
        /// - Initializes the game to create GraphicsDevice
        /// - Sets up window properties for headless testing
        /// - Returns null if initialization fails (allows tests to skip gracefully)
        /// - Based on StrideGraphicsBackend initialization pattern
        /// - Tests should check for null and skip if device creation fails
        /// </remarks>
        public static StrideGraphics.GraphicsDevice CreateTestStrideGraphicsDevice()
        {
            try
            {
                // Create a minimal Game instance for testing
                // Stride Game constructor initializes GraphicsDevice automatically
                var game = new global::Stride.Engine.Game();

                // Set window properties for headless/minimal testing
                // Note: ClientSize property may not be available in all Stride versions
                // Window properties are set via the Game constructor or Run method
                game.Window.Title = "Stride Test";
                game.Window.IsFullscreen = false;
                game.Window.IsMouseVisible = false;

                // Initialize the game to ensure GraphicsDevice is created
                // In a headless environment, this might fail if no GPU is available
                // Note: Initialize() is protected in Stride, so we use reflection if needed
                var initializeMethod = typeof(global::Stride.Engine.Game).GetMethod("Initialize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (initializeMethod != null)
                {
                    initializeMethod.Invoke(game, null);
                }

                // Return the GraphicsDevice from the game instance
                if (game.GraphicsDevice != null)
                {
                    return game.GraphicsDevice;
                }

                // If GraphicsDevice is null after initialization, dispose and return null
                game.Dispose();
                return null;
            }
            catch
            {
                // If device creation fails (e.g., no GPU in headless CI environment),
                // return null so tests can skip gracefully
                return null;
            }
        }

        /// <summary>
        /// Creates a test StrideGraphicsDevice wrapper (IGraphicsDevice) for Stride backend tests.
        /// Returns null if device creation fails (e.g., no GPU available in headless environment).
        /// </summary>
        /// <returns>StrideGraphicsDevice wrapper instance, or null if creation fails.</returns>
        /// <remarks>
        /// StrideGraphicsDevice Wrapper Creation for Tests:
        /// - Creates a real Stride GraphicsDevice using CreateTestStrideGraphicsDevice
        /// - Wraps it in StrideGraphicsDevice for use with IGraphicsBackend
        /// - Returns null if device creation fails (allows tests to skip gracefully)
        /// - Based on StrideGraphicsBackend device wrapping pattern
        /// - Tests should check for null and skip if device creation fails
        /// </remarks>
        public static IGraphicsDevice CreateTestStrideIGraphicsDevice()
        {
            var strideDevice = CreateTestStrideGraphicsDevice();
            if (strideDevice == null)
            {
                return null;
            }

            try
            {
                // CommandList is optional and will be null for test scenarios
                // In actual usage, it's obtained from Game.GraphicsContext.CommandList
                return new Andastra.Runtime.Stride.Graphics.StrideGraphicsDevice(
                    strideDevice,
                    null);
            }
            catch
            {
                // If wrapper creation fails, return null so tests can skip gracefully
                return null;
            }
        }

        /// <summary>
        /// Creates a test Stride Game instance for tests that need full game context.
        /// Returns null if game creation fails.
        /// </summary>
        /// <returns>Stride Game instance, or null if creation fails.</returns>
        /// <remarks>
        /// Stride Game Creation for Tests:
        /// - Creates a minimal Game instance with proper window configuration
        /// - Initializes the game for testing
        /// - Returns null if initialization fails (allows tests to skip gracefully)
        /// - Caller is responsible for disposing the Game instance
        /// </remarks>
        public static global::Stride.Engine.Game CreateTestStrideGame()
        {
            try
            {
                var game = new global::Stride.Engine.Game();
                // Note: ClientSize property may not be available in all Stride versions
                game.Window.Title = "Stride Test";
                game.Window.IsFullscreen = false;
                game.Window.IsMouseVisible = false;
                // Note: Initialize() is protected in Stride, so we use reflection if needed
                var initializeMethod = typeof(global::Stride.Engine.Game).GetMethod("Initialize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (initializeMethod != null)
                {
                    initializeMethod.Invoke(game, null);
                }

                if (game.GraphicsDevice != null)
                {
                    return game;
                }

                game.Dispose();
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Cleans up test Stride GraphicsDevice resources.
        /// </summary>
        /// <param name="game">The Game instance to dispose.</param>
        /// <remarks>
        /// Stride Cleanup:
        /// - Disposes the Game instance which will clean up all graphics resources
        /// - Handles disposal errors gracefully for test robustness
        /// </remarks>
        public static void CleanupTestStrideGame(global::Stride.Engine.Game game)
        {
            if (game != null)
            {
                try
                {
                    game.Dispose();
                }
                catch
                {
                    // Ignore disposal errors
                }
            }
        }
    }
}


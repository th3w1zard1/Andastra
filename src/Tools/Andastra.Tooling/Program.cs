using System;
using System.CommandLine;
using System.IO;
using System.Linq;
using Andastra.Parsing.Installation;
using Andastra.Parsing.Resource;
using Andastra.Runtime.Scripting.VM;
using Andastra.Runtime.Scripting.Interfaces;

namespace Andastra.Runtime.Tooling
{
    /// <summary>
    /// CLI entry point for Odyssey tooling commands.
    /// </summary>
    public class Program
    {
        public static int Main(string[] args)
        {
            var rootCommand = new RootCommand("Odyssey Engine Tooling CLI")
            {
                Description = "Headless import/validation commands for Odyssey Engine"
            };

            // validate-install command
            var pathOption = new Option<DirectoryInfo>(
                "--path",
                "Path to the KOTOR installation directory"
            )
            {
                Required = true
            };
            var validateCommand = new Command("validate-install", "Validate a KOTOR installation");
            validateCommand.Options.Add(pathOption);
            rootCommand.Add(validateCommand);

            // warm-cache command
            var modulePath = new Option<string>("--module", "Module resref to convert");
            var installPath = new Option<DirectoryInfo>("--install", "KOTOR installation path");
            var warmCacheCommand = new Command("warm-cache", "Pre-convert assets for a module");
            warmCacheCommand.Options.Add(modulePath);
            warmCacheCommand.Options.Add(installPath);
            warmCacheCommand.SetAction(parseResult =>
            {
                var module = parseResult.GetValue(modulePath);
                var install = parseResult.GetValue(installPath);
                WarmCache(module, install);
            });
            rootCommand.Add(warmCacheCommand);

            // dump-resource command
            var resRefOption = new Option<string>("--resref", "Resource reference name");
            var resTypeOption = new Option<string>("--type", "Resource type (e.g., utc, ncs, mdl)");
            var dumpInstallPath = new Option<DirectoryInfo>("--install", "KOTOR installation path");
            var dumpCommand = new Command("dump-resource", "Dump a resource (raw and decoded)");
            dumpCommand.Options.Add(resRefOption);
            dumpCommand.Options.Add(resTypeOption);
            dumpCommand.Options.Add(dumpInstallPath);
            rootCommand.Add(dumpCommand);

            // run-script command
            var scriptPath = new Option<FileInfo>("--script", "Path to NCS file")
            {
                Required = true
            };
            var runScriptCommand = new Command("run-script", "Execute an NCS script with mocked world");
            runScriptCommand.Options.Add(scriptPath);
            runScriptCommand.SetAction(parseResult =>
            {
                var script = parseResult.GetValue(scriptPath);
                RunScript(script);
            });
            rootCommand.Add(runScriptCommand);

            try
            {
                var parseResult = rootCommand.Parse(args);
                return parseResult.Invoke();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        private static void ValidateInstall(DirectoryInfo path)
        {
            Console.WriteLine("Validating installation at: " + path.FullName);

            if (!path.Exists)
            {
                Console.Error.WriteLine("ERROR: Directory does not exist.");
                Environment.ExitCode = 1;
                return;
            }

            // Check for chitin.key
            string chitinPath = Path.Combine(path.FullName, "chitin.key");
            if (!File.Exists(chitinPath))
            {
                Console.Error.WriteLine("ERROR: chitin.key not found. Not a valid KOTOR installation.");
                Environment.ExitCode = 1;
                return;
            }

            Console.WriteLine("Found chitin.key");

            // Check for data directory
            string dataPath = Path.Combine(path.FullName, "data");
            if (Directory.Exists(dataPath))
            {
                Console.WriteLine("Found data directory");
            }

            // Check for modules directory
            string modulesPath = Path.Combine(path.FullName, "modules");
            if (Directory.Exists(modulesPath))
            {
                int moduleCount = Directory.GetFiles(modulesPath, "*.rim").Length;
                moduleCount += Directory.GetFiles(modulesPath, "*.mod").Length;
                Console.WriteLine("Found modules directory with " + moduleCount + " module files");
            }

            // Check for override directory
            string overridePath = Path.Combine(path.FullName, "override");
            if (Directory.Exists(overridePath))
            {
                Console.WriteLine("Found override directory");
            }

            // Determine game type (K1 vs K2)
            string swkotorPath = Path.Combine(path.FullName, "swkotor.exe");
            string swkotor2Path = Path.Combine(path.FullName, "swkotor2.exe");

            if (File.Exists(swkotorPath))
            {
                Console.WriteLine("Detected: KOTOR 1");
            }
            else if (File.Exists(swkotor2Path))
            {
                Console.WriteLine("Detected: KOTOR 2 (TSL)");
            }
            else
            {
                Console.WriteLine("Game type: Unknown (no executable found)");
            }

            Console.WriteLine("Validation complete.");
        }

        private static void WarmCache(string module, DirectoryInfo install)
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(module))
            {
                Console.Error.WriteLine("ERROR: Module resref (--module) is required.");
                Environment.ExitCode = 1;
                return;
            }

            if (install == null || !install.Exists)
            {
                Console.Error.WriteLine("ERROR: Installation path (--install) is required and must exist.");
                Environment.ExitCode = 1;
                return;
            }

            Console.WriteLine("Warming cache for module: " + module);
            Console.WriteLine("Installation: " + install.FullName);

            try
            {
                // Create installation object
                Installation installation;
                try
                {
                    installation = new Installation(install.FullName);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"ERROR: Failed to load installation: {ex.Message}");
                    Environment.ExitCode = 1;
                    return;
                }

                Console.WriteLine($"Detected game: {installation.Game}");

                // Create module object
                Andastra.Parsing.Common.Module parsingModule;
                try
                {
                    parsingModule = new Andastra.Parsing.Common.Module(module, installation);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"ERROR: Failed to load module '{module}': {ex.Message}");
                    Environment.ExitCode = 1;
                    return;
                }

                Console.WriteLine($"Module loaded: {parsingModule.Root}");
                Console.WriteLine($"Module resources: {parsingModule.Resources.Count}");

                // Resources that typically need conversion/caching
                // Based on swkotor2.exe: MDL (models), TPC (textures), WOK (walkmeshes),
                // LYT (layouts), VIS (visibility), GIT (area instances), ARE (area properties)
                var resourceTypesToCache = new[]
                {
                    ResourceType.MDL,  // 3D models
                    ResourceType.TPC,  // Textures
                    ResourceType.WOK,   // Walkmeshes
                    ResourceType.LYT,   // Layouts
                    ResourceType.VIS,   // Visibility
                    ResourceType.GIT,   // Area instances
                    ResourceType.ARE,   // Area properties
                    ResourceType.IFO,   // Module info
                    ResourceType.NCS,   // Scripts (for validation)
                    ResourceType.DLG,   // Dialogues
                    ResourceType.UTC,   // Creatures
                    ResourceType.UTI,   // Items
                    ResourceType.UTP,   // Placeables
                    ResourceType.UTW,   // Waypoints
                    ResourceType.UTS,   // Sounds
                    ResourceType.UTM,   // Merchants
                    ResourceType.UTE,   // Encounters
                    ResourceType.UTT,   // Triggers
                };

                int totalResources = 0;
                int cachedResources = 0;
                int failedResources = 0;
                int skippedResources = 0;

                // Iterate through all resources in the module
                foreach (var resourceEntry in parsingModule.Resources)
                {
                    ResourceIdentifier resourceId = resourceEntry.Key;
                    ResourceType resourceType = resourceId.ResType;

                    // Only process resources that need caching
                    if (!resourceTypesToCache.Contains(resourceType))
                    {
                        skippedResources++;
                        continue;
                    }

                    totalResources++;

                    try
                    {
                        // Get resource data from installation
                        var resourceResult = parsingModule.Installation.Resource(resourceId.ResName, resourceType);
                        if (resourceResult == null || resourceResult.Data == null || resourceResult.Data.Length == 0)
                        {
                            Console.WriteLine($"  Skipping {resourceId.ResName}.{resourceType.Extension}: No data");
                            skippedResources++;
                            continue;
                        }

                        byte[] resourceData = resourceResult.Data;

                        // Attempt to load/decode the resource to validate and trigger conversion
                        // This will populate any internal caches
                        try
                        {
                            object decodedResource = ResourceAuto.LoadResource(resourceData, resourceType);
                            if (decodedResource != null)
                            {
                                // Resource successfully loaded/decoded - this triggers any conversion logic
                                // The ContentCache system will handle caching if configured
                                cachedResources++;
                                if (totalResources % 10 == 0)
                                {
                                    Console.WriteLine($"  Processed {totalResources} resources... ({cachedResources} cached, {failedResources} failed)");
                                }
                            }
                            else
                            {
                                // Resource exists but couldn't be decoded - still count as processed
                                Console.WriteLine($"  Warning: {resourceId.ResName}.{resourceType.Extension} could not be decoded");
                                skippedResources++;
                            }
                        }
                        catch (Exception decodeEx)
                        {
                            // Decoding failed - log but continue
                            Console.WriteLine($"  Warning: Failed to decode {resourceId.ResName}.{resourceType.Extension}: {decodeEx.Message}");
                            failedResources++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  Error processing {resourceId.ResName}.{resourceType.Extension}: {ex.Message}");
                        failedResources++;
                    }
                }

                Console.WriteLine();
                Console.WriteLine("Cache warming complete.");
                Console.WriteLine($"  Total resources processed: {totalResources}");
                Console.WriteLine($"  Successfully cached: {cachedResources}");
                Console.WriteLine($"  Failed: {failedResources}");
                Console.WriteLine($"  Skipped: {skippedResources}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERROR: Unexpected error during cache warming: {ex.Message}");
                Console.Error.WriteLine($"Stack trace: {ex.StackTrace}");
                Environment.ExitCode = 1;
            }
        }

        private static void DumpResource(string resref, string type, DirectoryInfo install)
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(resref))
            {
                Console.Error.WriteLine("ERROR: Resource reference (--resref) is required.");
                Environment.ExitCode = 1;
                return;
            }

            if (string.IsNullOrWhiteSpace(type))
            {
                Console.Error.WriteLine("ERROR: Resource type (--type) is required.");
                Environment.ExitCode = 1;
                return;
            }

            if (install == null || !install.Exists)
            {
                Console.Error.WriteLine("ERROR: Installation path (--install) is required and must exist.");
                Environment.ExitCode = 1;
                return;
            }

            Console.WriteLine("Dumping resource: " + resref + "." + type);
            Console.WriteLine("Installation: " + install.FullName);

            try
            {
                // Parse resource type
                ResourceType resourceType;
                try
                {
                    resourceType = ResourceType.FromExtension(type);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"ERROR: Invalid resource type '{type}': {ex.Message}");
                    Environment.ExitCode = 1;
                    return;
                }

                // Create installation object
                Installation installation;
                try
                {
                    installation = new Installation(install.FullName);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"ERROR: Failed to load installation: {ex.Message}");
                    Environment.ExitCode = 1;
                    return;
                }

                Console.WriteLine($"Detected game: {installation.Game}");

                // Lookup resource
                var resourceResult = installation.Resources.LookupResource(resref, resourceType);
                if (resourceResult == null || resourceResult.Data == null || resourceResult.Data.Length == 0)
                {
                    Console.Error.WriteLine($"ERROR: Resource '{resref}.{type}' not found in installation.");
                    Environment.ExitCode = 1;
                    return;
                }

                Console.WriteLine($"Found resource at: {resourceResult.FilePath}");
                Console.WriteLine($"Resource size: {resourceResult.Data.Length} bytes");

                // Determine output directory (use current directory)
                string outputDir = Directory.GetCurrentDirectory();
                string rawFileName = $"{resref}.{resourceType.Extension}";
                string rawFilePath = Path.Combine(outputDir, rawFileName);

                // Save raw bytes
                try
                {
                    File.WriteAllBytes(rawFilePath, resourceResult.Data);
                    Console.WriteLine($"Saved raw resource to: {rawFilePath}");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"ERROR: Failed to save raw resource: {ex.Message}");
                    Environment.ExitCode = 1;
                    return;
                }

                // Try to decode and save decoded version
                try
                {
                    object decodedResource = ResourceAuto.LoadResource(resourceResult.Data, resourceType);
                    if (decodedResource != null)
                    {
                        // Try to serialize the decoded resource back to bytes
                        byte[] decodedBytes = ResourceAuto.SaveResource(decodedResource, resourceType);
                        if (decodedBytes != null && decodedBytes.Length > 0)
                        {
                            string decodedFileName = $"{resref}.decoded.{resourceType.Extension}";
                            string decodedFilePath = Path.Combine(outputDir, decodedFileName);
                            File.WriteAllBytes(decodedFilePath, decodedBytes);
                            Console.WriteLine($"Saved decoded resource to: {decodedFilePath}");
                            Console.WriteLine($"Decoded resource size: {decodedBytes.Length} bytes");

                            // Compare sizes
                            if (decodedBytes.Length != resourceResult.Data.Length)
                            {
                                Console.WriteLine($"NOTE: Decoded size ({decodedBytes.Length}) differs from raw size ({resourceResult.Data.Length})");
                            }
                        }
                        else
                        {
                            Console.WriteLine("WARNING: Could not serialize decoded resource to bytes. Skipping decoded output.");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"WARNING: Could not decode resource of type '{type}'. Only raw bytes saved.");
                    }
                }
                catch (Exception ex)
                {
                    // Decoding is optional, so we don't fail if it doesn't work
                    Console.WriteLine($"WARNING: Failed to decode resource: {ex.Message}");
                    Console.WriteLine("Raw resource was saved successfully.");
                }

                Console.WriteLine("Resource dumping complete.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERROR: Unexpected error during resource dumping: {ex.Message}");
                Console.Error.WriteLine($"Stack trace: {ex.StackTrace}");
                Environment.ExitCode = 1;
            }
        }

        /// <summary>
        /// Executes an NCS script file with a mocked world environment.
        /// </summary>
        /// <remarks>
        /// Script Execution:
        /// - Based on swkotor2.exe: NCS VM execution system
        /// - Loads NCS bytecode from file and executes via NCS VM
        /// - Creates mocked world, entity, engine API, and script globals for script execution
        /// - Provides minimal but functional environment for testing script execution
        /// - Prints script output and execution statistics
        /// </remarks>
        private static void RunScript(FileInfo script)
        {
            Console.WriteLine("Running script: " + (script?.FullName ?? "not specified"));

            if (script == null || !script.Exists)
            {
                Console.Error.WriteLine("ERROR: Script file not found.");
                Environment.ExitCode = 1;
                return;
            }

            try
            {
                // Load NCS bytecode from file
                byte[] ncsBytes;
                try
                {
                    ncsBytes = File.ReadAllBytes(script.FullName);
                    Console.WriteLine($"Loaded NCS file: {ncsBytes.Length} bytes");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"ERROR: Failed to read script file: {ex.Message}");
                    Environment.ExitCode = 1;
                    return;
                }

                // Validate NCS header
                if (ncsBytes.Length < 13)
                {
                    Console.Error.WriteLine("ERROR: NCS file is too small (invalid format).");
                    Environment.ExitCode = 1;
                    return;
                }

                if (ncsBytes[0] != 'N' || ncsBytes[1] != 'C' || ncsBytes[2] != 'S' || ncsBytes[3] != ' ')
                {
                    Console.Error.WriteLine("ERROR: Invalid NCS signature (expected 'NCS ').");
                    Environment.ExitCode = 1;
                    return;
                }

                if (ncsBytes[4] != 'V' || ncsBytes[5] != '1' || ncsBytes[6] != '.' || ncsBytes[7] != '0')
                {
                    Console.Error.WriteLine("ERROR: Invalid NCS version (expected 'V1.0').");
                    Environment.ExitCode = 1;
                    return;
                }

                // Create mocked execution environment
                Console.WriteLine("Creating mocked execution environment...");

                // Create mock world for script execution environment
                var world = new MockWorld();

                // Create mock entity (caller) - represents the object executing the script
                var caller = new MockEntity("SCRIPT_CALLER");
                world.RegisterEntity(caller);

                // Create mock engine API - provides NWScript function implementations for script execution
                // MockEngineApi extends BaseEngineApi with comprehensive function implementations
                // Based on swkotor2.exe: Engine API function dispatch system
                // Provides implementations for common NWScript functions used in script testing
                var engineApi = new MockEngineApi();

                // Create script globals
                var globals = new Andastra.Runtime.Scripting.VM.ScriptGlobals();

                // Create execution context
                var context = new ExecutionContext(caller, world, engineApi, globals);
                // Set resource provider to null (scripts won't be able to load other scripts via ExecuteScript)
                context.ResourceProvider = null;

                // Create NCS VM
                var vm = new NcsVm();
                vm.MaxInstructions = 100000; // Default instruction limit
                vm.EnableTracing = false; // Disable tracing by default (can be enabled for debugging)

                Console.WriteLine("Executing script...");
                Console.WriteLine("--- Script Output ---");

                // Execute script
                int returnValue;
                try
                {
                    returnValue = vm.Execute(ncsBytes, context);

                    Console.WriteLine("--- End Script Output ---");
                    Console.WriteLine($"Script execution completed.");
                    Console.WriteLine($"Return value: {returnValue}");
                    Console.WriteLine($"Instructions executed: {vm.InstructionsExecuted}");

                    if (vm.InstructionsExecuted >= vm.MaxInstructions)
                    {
                        Console.WriteLine("WARNING: Script hit instruction limit (possible infinite loop).");
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"ERROR: Script execution failed: {ex.Message}");
                    Console.Error.WriteLine($"Stack trace: {ex.StackTrace}");
                    Environment.ExitCode = 1;
                    return;
                }

                Console.WriteLine("Script execution complete.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERROR: Unexpected error during script execution: {ex.Message}");
                Console.Error.WriteLine($"Stack trace: {ex.StackTrace}");
                Environment.ExitCode = 1;
            }
        }
    }
}


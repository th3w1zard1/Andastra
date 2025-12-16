using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Threading.Tasks;

namespace BioWareEngines.Tooling
{
    /// <summary>
    /// CLI entry point for Odyssey tooling commands.
    /// </summary>
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var rootCommand = new RootCommand("Odyssey Engine Tooling CLI")
            {
                Description = "Headless import/validation commands for Odyssey Engine"
            };

            // validate-install command
            var validateCommand = new Command("validate-install", "Validate a KOTOR installation");
            var pathOption = new Option<DirectoryInfo>(
                "--path",
                "Path to the KOTOR installation directory"
            );
            pathOption.IsRequired = true;
            validateCommand.AddOption(pathOption);
            validateCommand.SetHandler(ValidateInstall, pathOption);
            rootCommand.AddCommand(validateCommand);

            // warm-cache command
            var warmCacheCommand = new Command("warm-cache", "Pre-convert assets for a module");
            var modulePath = new Option<string>("--module", "Module resref to convert");
            var installPath = new Option<DirectoryInfo>("--install", "KOTOR installation path");
            warmCacheCommand.AddOption(modulePath);
            warmCacheCommand.AddOption(installPath);
            warmCacheCommand.SetHandler(WarmCache, modulePath, installPath);
            rootCommand.AddCommand(warmCacheCommand);

            // dump-resource command
            var dumpCommand = new Command("dump-resource", "Dump a resource (raw and decoded)");
            var resRefOption = new Option<string>("--resref", "Resource reference name");
            var resTypeOption = new Option<string>("--type", "Resource type (e.g., utc, ncs, mdl)");
            var dumpInstallPath = new Option<DirectoryInfo>("--install", "KOTOR installation path");
            dumpCommand.AddOption(resRefOption);
            dumpCommand.AddOption(resTypeOption);
            dumpCommand.AddOption(dumpInstallPath);
            dumpCommand.SetHandler(DumpResource, resRefOption, resTypeOption, dumpInstallPath);
            rootCommand.AddCommand(dumpCommand);

            // run-script command
            var runScriptCommand = new Command("run-script", "Execute an NCS script with mocked world");
            var scriptPath = new Option<FileInfo>("--script", "Path to NCS file");
            runScriptCommand.AddOption(scriptPath);
            runScriptCommand.SetHandler(RunScript, scriptPath);
            rootCommand.AddCommand(runScriptCommand);

            return await rootCommand.InvokeAsync(args);
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
            Console.WriteLine("Warming cache for module: " + module);
            Console.WriteLine("Installation: " + (install?.FullName ?? "not specified"));
            
            // TODO: Implement cache warming
            Console.WriteLine("Cache warming not yet implemented.");
        }

        private static void DumpResource(string resref, string type, DirectoryInfo install)
        {
            Console.WriteLine("Dumping resource: " + resref + "." + type);
            Console.WriteLine("Installation: " + (install?.FullName ?? "not specified"));
            
            // TODO: Implement resource dumping
            Console.WriteLine("Resource dumping not yet implemented.");
        }

        private static void RunScript(FileInfo script)
        {
            Console.WriteLine("Running script: " + (script?.FullName ?? "not specified"));
            
            if (script == null || !script.Exists)
            {
                Console.Error.WriteLine("ERROR: Script file not found.");
                Environment.ExitCode = 1;
                return;
            }

            // TODO: Implement script execution
            Console.WriteLine("Script execution not yet implemented.");
        }
    }
}


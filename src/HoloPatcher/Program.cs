using System;
using System.IO;
using System.Linq;
using Avalonia;
using TSLPatcher.Core.Common;
using TSLPatcher.Core.Logger;
using TSLPatcher.Core.Namespaces;
using TSLPatcher.Core.Patcher;
using TSLPatcher.Core.Reader;

namespace HoloPatcher;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length > 0 && (args.Contains("--cli") || args.Contains("--help")))
        {
            RunCli(args);
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static void RunCli(string[] args)
    {
        string? modPath = null;
        string? gamePath = null;
        string? namespaceName = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--mod" && i + 1 < args.Length)
            {
                modPath = args[++i];
            }
            else if (args[i] == "--game" && i + 1 < args.Length)
            {
                gamePath = args[++i];
            }
            else if (args[i] == "--namespace" && i + 1 < args.Length)
            {
                namespaceName = args[++i];
            }
        }

        if (args.Contains("--help") || modPath == null || gamePath == null)
        {
            Console.WriteLine("HoloPatcher CLI Usage:");
            Console.WriteLine("  HoloPatcher --cli --mod \"path/to/mod\" --game \"path/to/game\" [--namespace \"Default\"]");
            return;
        }

        Console.WriteLine($"Mod Path: {modPath}");
        Console.WriteLine($"Game Path: {gamePath}");

        var logger = new PatchLogger();
        logger.VerboseLogged += (s, l) => Console.WriteLine($"[VERBOSE] {l.Message}");
        logger.NoteLogged += (s, l) => Console.WriteLine($"[NOTE] {l.Message}");
        logger.WarningLogged += (s, l) => Console.WriteLine($"[WARNING] {l.Message}");
        logger.ErrorLogged += (s, l) => Console.Error.WriteLine($"[ERROR] {l.Message}");

        try
        {
            // Load namespaces logic
            string tslPatchDataPath = Path.Combine(modPath, "tslpatchdata");
            if (!Directory.Exists(tslPatchDataPath))
            {
                // Try modPath as tslpatchdata root if explicit folder missing
                tslPatchDataPath = modPath;
            }

            string namespacesIniPath = Path.Combine(tslPatchDataPath, "namespaces.ini");
            string changesIniPath;

            if (File.Exists(namespacesIniPath))
            {
                var namespaces = NamespaceReader.FromFilePath(namespacesIniPath);
                PatcherNamespace? ns = null;

                if (!string.IsNullOrEmpty(namespaceName))
                {
                    ns = namespaces.FirstOrDefault(n => n.Name.Equals(namespaceName, StringComparison.OrdinalIgnoreCase));
                }

                ns ??= namespaces.FirstOrDefault();

                if (ns == null)
                {
                    Console.WriteLine("No namespaces found in namespaces.ini.");
                    return;
                }

                changesIniPath = Path.Combine(tslPatchDataPath, ns.ChangesFilePath());
                Console.WriteLine($"Using namespace: {ns.Name} ({ns.ChangesFilePath()})");
            }
            else
            {
                changesIniPath = Path.Combine(tslPatchDataPath, "changes.ini");
                if (!File.Exists(changesIniPath))
                {
                    // Try one level up if tslpatchdata was assumed
                    changesIniPath = Path.Combine(modPath, "changes.ini");
                }
            }

            if (!File.Exists(changesIniPath))
            {
                Console.WriteLine($"Changes INI not found at: {changesIniPath}");
                return;
            }

            var installer = new ModInstaller(modPath, gamePath, changesIniPath, logger);
            installer.TslPatchDataPath = tslPatchDataPath;

            Console.WriteLine("Starting installation...");
            installer.Install(progressCallback: (p) => Console.Write("."));
            Console.WriteLine("\nInstallation completed successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nInstallation failed: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            Environment.Exit(1);
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}

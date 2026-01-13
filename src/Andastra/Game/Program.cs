using System;
using BioWare.NET.Common;
using Andastra.Runtime.Core;
using Andastra.Game.Core;
using Andastra.Runtime.Graphics;
using Andastra.Runtime.Graphics.Common.Enums;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace Andastra.Game
{
    /// <summary>
    /// Entry point for the Odyssey Engine game launcher.
    /// </summary>
    /// <remarks>
    /// Program Entry Point:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): entry @ 0x0076e2dd (PE entry point)
    /// - Main initialization: 0x00404250 @ 0x00404250 (WinMain equivalent, initializes game)
    /// - Located via string references: "swkotor2" @ 0x007b575c (executable name), "KotOR2" @ 0x0080c210 (BioWareGame title)
    /// - Original implementation: Entry point calls GetVersionExA, initializes heap, calls 0x00404250
    /// - 0x00404250 @ 0x00404250: Creates mutex "swkotor2" via CreateMutexA, initializes COM via CoInitialize, loads config.txt (0x00460ff0), loads swKotor2.ini (0x00630a90), creates engine objects, runs game loop
    /// - Mutex creation: CreateMutexA with name "swkotor2" prevents multiple instances, WaitForSingleObject checks if already running
    /// - Config loading: 0x00460ff0 @ 0x00460ff0 loads and executes text files (config.txt, startup.txt)
    /// - INI loading: 0x00630a90 @ 0x00630a90 loads INI file values, 0x00631ea0 @ 0x00631ea0 parses INI sections, 0x00630c20 cleans up INI structures
    /// - Sound initialization: Checks "Disable Sound" setting from INI, sets DAT_008b73c0 flag
    /// - Window creation: 0x00403f70 creates main window, 0x004015b0/0x00401610 initialize graphics
    /// - Game loop: PeekMessageA/GetMessageA for Windows message processing, TranslateMessage/DispatchMessageA for input
    /// - Game initialization: Detects KOTOR installation path, loads configuration, creates game instance
    /// - Command line: DAT_008ba024 = GetCommandLineA() stores command-line arguments
    /// - Exit: Returns 0 on success, 0xffffffff if mutex already exists, 1 on error
    /// </remarks>
    public static class Program
    {
        public static Andastra.Game.GUI.GameLauncher _staticLauncher;

        [STAThread]
        public static int Main(string[] args)
        {
            try
            {
                // Check for --no-launcher flag to skip launcher UI
                bool skipLauncher = false;
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "--no-launcher" || args[i] == "-n")
                    {
                        skipLauncher = true;
                        break;
                    }
                }

                GameSettings settings = null;
                string gamePath = null;
                BioWareGame selectedGame = BioWareGame.K1;

                if (!skipLauncher)
                {
                    // Run Avalonia launcher and wait for result
                    BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, Avalonia.Controls.ShutdownMode.OnMainWindowClose);

                    if (_staticLauncher == null || !_staticLauncher.StartClicked)
                    {
                        return 0; // User cancelled
                    }

                    selectedGame = _staticLauncher.SelectedGame;
                    gamePath = _staticLauncher.SelectedPath;

                    // Check if this is a KOTOR game or another BioWare game
                    if (selectedGame.IsOdyssey())
                    {
                        // Convert BioWareGame to KotorGame for Odyssey/KOTOR games
                        KotorGame kotorGame = KotorGame.K1;
                        if (selectedGame.IsK2())
                        {
                            kotorGame = KotorGame.K2;
                        }

                        settings = new GameSettings
                        {
                            Game = kotorGame,
                            GamePath = gamePath
                        };
                    }
                    else
                    {
                        // For non-KOTOR games, use unified launcher
                        // GameSettings is only for KOTOR games, so we'll handle non-KOTOR games separately
                        settings = null;
                    }
                }
                else
                {
                    // Parse command line arguments (legacy mode)
                    // Note: Command-line mode currently only supports KOTOR games
                    settings = GameSettingsExtensions.FromCommandLine(args);

                    // Detect KOTOR installation if not specified
                    if (string.IsNullOrEmpty(settings.GamePath))
                    {
                        settings.GamePath = GamePathDetector.DetectKotorPath(settings.Game);
                        if (string.IsNullOrEmpty(settings.GamePath))
                        {
                            Console.Error.WriteLine("ERROR: Could not detect KOTOR installation.");
                            Console.Error.WriteLine("Please specify the game path with --path <path>");
                            return 1;
                        }
                    }

                    // Set selectedGame based on KotorGame for command-line mode
                    selectedGame = settings.Game == KotorGame.K2 ? BioWareGame.K2 : BioWareGame.K1;
                    gamePath = settings.GamePath;
                }

                // Determine graphics backend from launcher or command line
                GraphicsBackendType backendType = GraphicsBackendType.MonoGame; // Default fallback
                if (!skipLauncher && _staticLauncher != null)
                {
                    // Use backend selected in launcher UI
                    backendType = _staticLauncher.SelectedGraphicsBackend;
                }
                else
                {
                    // Check command line for backend override (for --no-launcher mode)
                    for (int i = 0; i < args.Length; i++)
                    {
                        if (args[i] == "--backend" && i + 1 < args.Length)
                        {
                            if (args[i + 1].Equals("stride", StringComparison.OrdinalIgnoreCase))
                            {
                                backendType = GraphicsBackendType.Stride;
                            }
                            else if (args[i + 1].Equals("monogame", StringComparison.OrdinalIgnoreCase))
                            {
                                backendType = GraphicsBackendType.MonoGame;
                            }
                            break;
                        }
                    }
                }

                // Launch the game
                try
                {
                    // Determine game type for OdysseyEngine backend
                    KotorGame? kotorGameType = null;
                    if (backendType == GraphicsBackendType.OdysseyEngine)
                    {
                        if (settings != null)
                        {
                            kotorGameType = settings.Game;
                        }
                        else if (selectedGame.IsOdyssey())
                        {
                            // Convert BioWareGame to KotorGame
                            if (selectedGame.IsK2())
                            {
                                kotorGameType = KotorGame.K2;
                            }
                            else if (selectedGame.IsK1())
                            {
                                kotorGameType = KotorGame.K1;
                            }
                        }

                        if (!kotorGameType.HasValue)
                        {
                            throw new InvalidOperationException("Game type (K1 or K2) is required when using OdysseyEngine backend");
                        }
                    }

                    // Create graphics backend
                    IGraphicsBackend graphicsBackend = Core.GraphicsBackendFactory.CreateBackend(backendType, kotorGameType);

                    // Check if this is a KOTOR game (uses OdysseyGame) or another BioWare game (uses UnifiedGameLauncher)
                    // For command-line mode, settings will always be set and will be for KOTOR games
                    // For launcher UI mode, check if it's an Odyssey game
                    if ((selectedGame.IsOdyssey() || settings != null) && settings != null)
                    {
                        // Use OdysseyGame for KOTOR games
                        using (var game = new OdysseyGame(settings, graphicsBackend))
                        {
                            game.Run();
                        }
                    }
                    else
                    {
                        // Use UnifiedGameLauncher for other BioWare games (Aurora, Eclipse, Infinity)
                        // Get game path from settings or use the path from launcher
                        string gamePathForLauncher = settings != null ? settings.GamePath : gamePath;

                        if (string.IsNullOrEmpty(gamePathForLauncher))
                        {
                            throw new InvalidOperationException($"Game path is required for {selectedGame}");
                        }

                        using (var launcher = new UnifiedGameLauncher(selectedGame, gamePathForLauncher, graphicsBackend, settings))
                        {
                            launcher.Initialize();
                            launcher.Run();
                        }
                    }

                    return 0;
                }
                catch (Exception ex)
                {
                    // Show error dialog (cross-platform)
                    string errorMessage = $"Failed to start the game:\n\n{ex.Message}";
                    if (ex.InnerException != null)
                    {
                        errorMessage += $"\n\nInner Exception: {ex.InnerException.Message}";
                    }
                    errorMessage += $"\n\nStack Trace:\n{ex.StackTrace}";

                    ShowErrorMessage(errorMessage);
                    return 1;
                }
            }
            catch (Exception ex)
            {
                // Fatal error in launcher itself
                ShowErrorMessage($"Fatal error in launcher:\n\n{ex.Message}\n\n{ex.StackTrace}");
                return 1;
            }
        }

        /// <summary>
        /// Builds the Avalonia application instance.
        /// </summary>
        /// <returns>The configured Avalonia application builder.</returns>
        private static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<AvaloniaApp>()
                .UsePlatformDetect()
                .LogToTrace();
        }

        /// <summary>
        /// Shows an error message to the user using native message box or console.
        /// </summary>
        /// <param name="message">The error message to display.</param>
        private static void ShowErrorMessage(string message)
        {
            // Try to use console first (if available)
            try
            {
                Console.Error.WriteLine(message);
            }
            catch
            {
                // Console not available
            }

            // TODO: SIMPLIFIED - For now, just write to console. Full implementation would show native message box.
            // Original engine shows message box via Windows MessageBoxA API
            // Future: Implement native message box for each platform (Windows: MessageBox, Linux: zenity, Mac: osascript)
        }
    }

    /// <summary>
    /// Avalonia application class for the game launcher.
    /// </summary>
    public class AvaloniaApp : Avalonia.Application
    {
        public override void Initialize()
        {
            // Initialize Avalonia theme
            Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                Program._staticLauncher = new Andastra.Game.GUI.GameLauncher();
                desktop.MainWindow = Program._staticLauncher;
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}

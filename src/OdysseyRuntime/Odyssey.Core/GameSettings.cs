namespace Odyssey.Core
{
    /// <summary>
    /// Which KOTOR game to run.
    /// </summary>
    public enum KotorGame
    {
        K1,
        K2
    }

    /// <summary>
    /// Game settings and configuration.
    /// </summary>
    /// <remarks>
    /// Game Settings:
    /// - Based on swkotor2.exe game configuration system
    /// - Located via string references: "swkotor2.ini" @ 0x007b5740, ".\swkotor2.ini" @ 0x007b5644, "config.txt" @ 0x007b5750
    /// - "swkotor.ini" (K1 config file), "DiffSettings" @ 0x007c2cdc (display settings)
    /// - Original implementation: Game settings loaded from INI file (swkotor2.ini for K2, swkotor.ini for K1)
    /// - Settings include: Game path, window size, fullscreen mode, graphics options, audio options
    /// - Command-line arguments override INI file settings
    /// - Based on swkotor2.exe: FUN_00633270 @ 0x00633270 (loads configuration from INI file)
    /// </remarks>
    public class GameSettings
    {
        /// <summary>
        /// Which game (K1 or K2).
        /// </summary>
        public KotorGame Game { get; set; } = KotorGame.K1;

        /// <summary>
        /// Path to the KOTOR installation.
        /// </summary>
        public string GamePath { get; set; }

        /// <summary>
        /// Starting module override (null = use default starting module).
        /// </summary>
        public string StartModule { get; set; }

        /// <summary>
        /// Save game to load (null = new game).
        /// </summary>
        public string LoadSave { get; set; }

        /// <summary>
        /// Window width.
        /// </summary>
        public int Width { get; set; } = 1280;

        /// <summary>
        /// Window height.
        /// </summary>
        public int Height { get; set; } = 720;

        /// <summary>
        /// Fullscreen mode.
        /// </summary>
        public bool Fullscreen { get; set; } = false;

        /// <summary>
        /// Enable debug rendering.
        /// </summary>
        public bool DebugRender { get; set; } = false;

        /// <summary>
        /// Skip intro videos.
        /// </summary>
        public bool SkipIntro { get; set; } = true;
    }
}


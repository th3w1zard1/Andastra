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


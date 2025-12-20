namespace Andastra.Runtime.Core
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

        /// <summary>
        /// Mouse sensitivity for camera controls.
        /// </summary>
        /// <remarks>
        /// Mouse Sensitivity Setting:
        /// - Based on swkotor.exe and swkotor2.exe mouse configuration system
        /// - Original implementation: Mouse sensitivity option in options menu (Controls category)
        /// - Controls how responsive mouse movement is for camera rotation/looking
        /// - Range: 0.0 (no sensitivity) to 1.0 (maximum sensitivity)
        /// - Applied to mouse delta input when processing camera controls
        /// - Based on swkotor2.exe: Mouse input scaling for camera control
        /// </remarks>
        public float MouseSensitivity { get; set; } = 0.5f;

        /// <summary>
        /// Invert mouse Y axis for camera movement.
        /// </summary>
        /// <remarks>
        /// Mouse Invert Setting:
        /// - Based on swkotor.exe and swkotor2.exe mouse configuration system
        /// - Original implementation: Mouse invert option in options menu (Controls category)
        /// - When enabled, mouse Y movement is inverted (moving mouse up moves camera down, and vice versa)
        /// - Applied to camera look/rotation input processing
        /// - Based on swkotor2.exe: Mouse input processing for camera control
        /// </remarks>
        public bool InvertMouseY { get; set; } = false;

        /// <summary>
        /// Applies mouse Y inversion to a mouse Y delta value if the setting is enabled.
        /// </summary>
        /// <param name="mouseYDelta">The raw mouse Y delta value.</param>
        /// <returns>The mouse Y delta with inversion applied if enabled, otherwise the original value.</returns>
        /// <remarks>
        /// Mouse Y Inversion:
        /// - When InvertMouseY is true, negates the mouse Y delta (inverts the axis)
        /// - Used in camera look/rotation input processing
        /// - Call this method wherever mouse Y delta is used for camera pitch/vertical rotation
        /// - Based on swkotor.exe and swkotor2.exe: Mouse invert implementation
        /// </remarks>
        public float ApplyMouseYInversion(float mouseYDelta)
        {
            return InvertMouseY ? -mouseYDelta : mouseYDelta;
        }

        /// <summary>
        /// Audio settings (volume levels for music, sound effects, and voice).
        /// </summary>
        /// <remarks>
        /// Audio Settings:
        /// - Based on swkotor2.exe audio configuration system
        /// - Located via string references: "MusicVolume" @ 0x007c2cdc, "SoundVolume" @ 0x007c2ce0, "VoiceVolume" @ 0x007c2ce4
        /// - Original implementation: Audio volumes stored in INI file (swkotor2.ini for K2, swkotor.ini for K1)
        /// - Volume range: 0.0 to 1.0 (0% to 100% in UI)
        /// - Based on swkotor2.exe: FUN_00631ff0 @ 0x00631ff0 (writes INI values for audio settings)
        /// - Based on swkotor2.exe: FUN_00633270 @ 0x00633270 (loads audio settings from INI file)
        /// </remarks>
        public AudioSettings Audio { get; set; } = new AudioSettings();

        /// <summary>
        /// Invert mouse Y axis for camera controls.
        /// </summary>
        /// <remarks>
        /// Mouse Invert Setting:
        /// - Based on swkotor.exe and swkotor2.exe: Mouse invert option in options menu
        /// - When enabled, mouse Y movement is inverted (moving mouse up looks down, moving mouse down looks up)
        /// - Original implementation: Stored in INI file as boolean value
        /// - Applied to camera pitch control when using mouse for camera rotation
        /// </remarks>
        public bool InvertMouseY { get; set; } = false;

        /// <summary>
        /// Audio settings configuration.
        /// </summary>
        public class AudioSettings
        {
            /// <summary>
            /// Master volume (0.0 to 1.0, affects all audio).
            /// </summary>
            public float MasterVolume { get; set; } = 1.0f;

            /// <summary>
            /// Music volume (0.0 to 1.0).
            /// </summary>
            public float MusicVolume { get; set; } = 0.8f;

            /// <summary>
            /// Sound effects volume (0.0 to 1.0).
            /// </summary>
            public float SfxVolume { get; set; } = 1.0f;

            /// <summary>
            /// Voice volume (0.0 to 1.0).
            /// </summary>
            public float VoiceVolume { get; set; } = 1.0f;

            /// <summary>
            /// Whether music is enabled.
            /// </summary>
            public bool MusicEnabled { get; set; } = true;
        }
    }
}

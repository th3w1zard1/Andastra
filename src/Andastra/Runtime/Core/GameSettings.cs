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
        /// Invert mouse Y axis.
        /// </summary>
        public bool InvertMouseY { get; set; } = false;

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
        /// Master volume (affects all audio).
        /// </summary>
        public float MasterVolume { get; set; } = 1.0f;

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
        /// Graphics settings (resolution, quality, display options).
        /// </summary>
        /// <remarks>
        /// Graphics Settings:
        /// - Based on swkotor.exe and swkotor2.exe graphics configuration system
        /// - Located via string references: "Width" @ 0x007c2cd0, "Height" @ 0x007c2cd4, "Fullscreen" @ 0x007c2cd8
        /// - Original implementation: Graphics settings stored in INI file (swkotor2.ini for K2, swkotor.ini for K1)
        /// - Settings include: Resolution, Fullscreen mode, Texture quality, Shadow quality, Anti-aliasing
        /// - Based on swkotor2.exe: FUN_00633270 @ 0x00633270 (loads graphics settings from INI file)
        /// - Based on swkotor2.exe: FUN_00631ff0 @ 0x00631ff0 (writes graphics settings to INI file)
        /// </remarks>
        public GraphicsSettings Graphics { get; set; } = new GraphicsSettings();

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

        /// <summary>
        /// Autopause settings configuration.
        /// </summary>
        /// <remarks>
        /// Autopause Settings:
        /// - Based on swkotor.exe and swkotor2.exe autopause system
        /// - Controls automatic pausing of the game under various conditions
        /// - Each setting corresponds to a different autopause trigger
        /// - Original implementation: Stored in INI file as boolean values
        /// - Based on swkotor2.exe: CSWGuiOptionsMain::OnAutopauseOpt handles autopause menu
        /// </remarks>
        public class AutopauseSettings
        {
            /// <summary>
            /// Pause when the game window loses focus.
            /// </summary>
            public bool PauseOnLostFocus { get; set; } = true;

            /// <summary>
            /// Pause when starting a conversation.
            /// </summary>
            public bool PauseOnConversation { get; set; } = false;

            /// <summary>
            /// Pause when opening containers.
            /// </summary>
            public bool PauseOnContainer { get; set; } = false;

            /// <summary>
            /// Pause when looting corpses.
            /// </summary>
            public bool PauseOnCorpse { get; set; } = false;

            /// <summary>
            /// Pause during area transitions.
            /// </summary>
            public bool PauseOnAreaTransition { get; set; } = false;

            /// <summary>
            /// Pause when a party member dies.
            /// </summary>
            public bool PauseOnPartyDeath { get; set; } = false;

            /// <summary>
            /// Pause when the player dies.
            /// </summary>
            public bool PauseOnPlayerDeath { get; set; } = true;
        }

        /// <summary>
        /// Feedback settings configuration.
        /// </summary>
        /// <remarks>
        /// Feedback Settings:
        /// - Based on swkotor.exe and swkotor2.exe feedback system
        /// - Controls visual and audio feedback during gameplay
        /// - Settings stored in INI file (swkotor.ini for K1, swkotor2.ini for K2)
        /// - Based on swkotor2.exe: CSWGuiOptionsMain::OnFeedbackOpt @ 0x006e2df0
        /// </remarks>
        public class FeedbackSettings
        {
            /// <summary>
            /// Show damage numbers during combat.
            /// </summary>
            public bool ShowDamageNumbers { get; set; } = true;

            /// <summary>
            /// Show hit/miss feedback during combat.
            /// </summary>
            public bool ShowHitMissFeedback { get; set; } = true;

            /// <summary>
            /// Show subtitles for dialogue.
            /// </summary>
            public bool ShowSubtitles { get; set; } = true;

            /// <summary>
            /// Show action queue feedback.
            /// </summary>
            public bool ShowActionQueue { get; set; } = true;

            /// <summary>
            /// Show minimap.
            /// </summary>
            public bool ShowMinimap { get; set; } = true;

            /// <summary>
            /// Show party member health bars.
            /// </summary>
            public bool ShowPartyHealthBars { get; set; } = true;

            /// <summary>
            /// Show floating combat text.
            /// </summary>
            public bool ShowFloatingCombatText { get; set; } = true;
        }

        /// <summary>
        /// Graphics settings configuration.
        /// </summary>
        public class GraphicsSettings
        {
            /// <summary>
            /// Window width in pixels.
            /// </summary>
            public int ResolutionWidth { get; set; } = 1920;

            /// <summary>
            /// Window height in pixels.
            /// </summary>
            public int ResolutionHeight { get; set; } = 1080;

            /// <summary>
            /// Whether to run in fullscreen mode.
            /// </summary>
            public bool Fullscreen { get; set; } = false;

            /// <summary>
            /// Whether vertical sync is enabled.
            /// </summary>
            public bool VSync { get; set; } = true;

            /// <summary>
            /// Texture quality level (0=Low, 1=Medium, 2=High).
            /// </summary>
            public int TextureQuality { get; set; } = 2;

            /// <summary>
            /// Shadow quality level (0=Off, 1=Low, 2=Medium, 3=High).
            /// </summary>
            public int ShadowQuality { get; set; } = 2;

            /// <summary>
            /// Anisotropic filtering level (0=Off, 2=2x, 4=4x, 8=8x, 16=16x).
            /// </summary>
            public int AnisotropicFiltering { get; set; } = 4;

            /// <summary>
            /// Whether anti-aliasing is enabled.
            /// </summary>
            public bool AntiAliasing { get; set; } = true;
        }

        /// <summary>
        /// Gameplay settings configuration.
        /// </summary>
        public class GameplaySettings
        {
            /// <summary>
            /// Whether auto-save is enabled.
            /// </summary>
            public bool AutoSave { get; set; } = true;

            /// <summary>
            /// Auto-save interval in seconds.
            /// </summary>
            public int AutoSaveInterval { get; set; } = 300;

            /// <summary>
            /// Whether tooltips are enabled.
            /// </summary>
            public bool Tooltips { get; set; } = true;

            /// <summary>
            /// Whether subtitles are enabled.
            /// </summary>
            public bool Subtitles { get; set; } = true;

            /// <summary>
            /// Dialogue speed multiplier (0.5x to 2.0x).
            /// </summary>
            public float DialogueSpeed { get; set; } = 1.0f;

            /// <summary>
            /// Whether to use classic controls.
            /// </summary>
            public bool ClassicControls { get; set; } = false;
        }

        /// <summary>
        /// Gameplay settings.
        /// </summary>
        public GameplaySettings Gameplay { get; set; } = new GameplaySettings();

        /// <summary>
        /// Autopause settings.
        /// </summary>
        public AutopauseSettings Autopause { get; set; } = new AutopauseSettings();

        /// <summary>
        /// Feedback settings configuration.
        /// </summary>
        /// <remarks>
        /// Feedback Settings:
        /// - Based on swkotor2.exe feedback options system
        /// - Located via string references: "BTN_FEEDBACK" (feedback button in options menu)
        /// - Based on swkotor2.exe: CSWGuiOptionsMain::OnFeedbackOpt @ 0x006e2df0 (feedback options handler)
        /// - Original implementation: Feedback options control tooltip display, combat feedback, and other UI feedback elements
        /// - Settings include: Tooltip display options, combat damage numbers, floating text, etc.
        /// - Based on swkotor2.exe: FUN_00631ff0 @ 0x00631ff0 (writes feedback settings to INI file)
        /// - Based on swkotor2.exe: FUN_00633270 @ 0x00633270 (loads feedback settings from INI file)
        /// </remarks>
        public FeedbackSettings Feedback { get; set; } = new FeedbackSettings();

        /// <summary>
        /// Feedback settings configuration.
        /// </summary>
        public class FeedbackSettings
        {
            /// <summary>
            /// Whether tooltips are enabled (show tooltips when hovering over items, abilities, etc.).
            /// </summary>
            public bool TooltipsEnabled { get; set; } = true;

            /// <summary>
            /// Tooltip delay in milliseconds before showing tooltip.
            /// </summary>
            public int TooltipDelay { get; set; } = 500;

            /// <summary>
            /// Whether to show combat damage numbers (floating damage text).
            /// </summary>
            public bool ShowCombatDamageNumbers { get; set; } = true;

            /// <summary>
            /// Whether to show combat feedback text (hit/miss/critical messages).
            /// </summary>
            public bool ShowCombatFeedback { get; set; } = true;

            /// <summary>
            /// Whether to show floating text for experience gains.
            /// </summary>
            public bool ShowExperienceGains { get; set; } = true;

            /// <summary>
            /// Whether to show floating text for item pickups.
            /// </summary>
            public bool ShowItemPickups { get; set; } = true;

            /// <summary>
            /// Whether to show quest update notifications.
            /// </summary>
            public bool ShowQuestUpdates { get; set; } = true;

            /// <summary>
            /// Whether to show skill check feedback (success/failure messages).
            /// </summary>
            public bool ShowSkillCheckFeedback { get; set; } = true;
        }
    }
}

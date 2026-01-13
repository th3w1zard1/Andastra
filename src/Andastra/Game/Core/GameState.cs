namespace Andastra.Runtime.Game.Core
{
    /// <summary>
    /// Represents the current state of the game.
    /// </summary>
    /// <remarks>
    /// Game State Enum:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) game state management system
    /// - Located via string references: "GameState" @ 0x007c15d0 (game state field), "GameMode" @ 0x007c15e0 (game mode field)
    /// - "GAMEINPROGRESS" @ 0x007c15c8 (game in progress flag), "ModuleLoaded" @ 0x007bdd70 (module loaded flag)
    /// - "ModuleRunning" @ 0x007bdd58 (module running flag)
    /// - Menu states: "RIMS:MAINMENU" @ 0x007b6044 (main menu RIM), "MAINMENU" @ 0x007cc030 (main menu constant)
    /// - "mainmenu_p" @ 0x007cc000 (main menu panel), "mainmenu01" @ 0x007cc108, "mainmenu02" @ 0x007cc138 (main menu variants)
    /// - "Action Menu" @ 0x007c8480 (action menu), "CB_ACTIONMENU" @ 0x007d29d4 (action menu checkbox)
    /// - Original implementation: Game state tracks current UI/mode (main menu, loading, in-game, paused, save/load menus)
    /// - State transitions: MainMenu -> Loading -> InGame, InGame -> Paused/SaveMenu/LoadMenu
    /// - Module state management: FUN_006caab0 @ 0x006caab0 sets module state flags in DAT_008283d4 structure
    ///   - State 0 (Idle): Sets `*(undefined2 *)(DAT_008283d4 + 4) = 0`, sets bit flag `*puVar6 | 1`
    ///   - State 1 (ModuleLoaded): Sets `*(undefined2 *)(DAT_008283d4 + 4) = 1`, sets bit flag `*puVar6 | 0x11` (0x10 | 0x1)
    ///   - State 2 (ModuleRunning): Sets `*(undefined2 *)(DAT_008283d4 + 4) = 2`, sets bit flag `*puVar6 | 0x1`
    ///   - Located via string references: "ModuleLoaded" @ 0x00826e24, "ModuleRunning" @ 0x00826e2c, "ServerStatus" @ 0x00826e1c
    ///   - Function signature: `undefined4 FUN_006caab0(char *param_1, int param_2)` - Parses server command strings like "S.Module.ModuleLoaded" or "S.Module.ModuleRunning"
    /// </remarks>
    public enum GameState
    {
        /// <summary>
        /// Main menu - player selects install path and starting module.
        /// </summary>
        MainMenu,

        /// <summary>
        /// Loading screen - game is loading module and initializing world.
        /// </summary>
        Loading,

        /// <summary>
        /// In game - player is actively playing.
        /// </summary>
        InGame,

        /// <summary>
        /// Paused - game is paused (in-game menu).
        /// </summary>
        Paused,

        /// <summary>
        /// Save menu - player is selecting a save slot.
        /// </summary>
        SaveMenu,

        /// <summary>
        /// Load menu - player is selecting a save to load.
        /// </summary>
        LoadMenu,

        /// <summary>
        /// Character creation - player is creating their character.
        /// </summary>
        /// <remarks>
        /// Character Creation State:
        /// - Based on swkotor.exe and swkotor2.exe character generation system
        /// - GUI Panel: "maincg" (character generation)
        /// - K1 Music: "mus_theme_rep", K2 Music: "mus_main"
        /// - Load Screen: K1 uses "load_chargen", K2 uses "load_default"
        /// - Flow: Main Menu → Character Creation → Module Load
        /// </remarks>
        CharacterCreation,

        /// <summary>
        /// Options menu - player is configuring game settings.
        /// </summary>
        /// <remarks>
        /// Options Menu State:
        /// - Based on swkotor.exe and swkotor2.exe options menu system
        /// - Located via string references: "BTN_OPTIONS" (options button in main menu)
        /// - GUI Panel: "optionsmain" (main menu options) or "optionsingame" (in-game options)
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): CSWGuiOptionsMain class @ 0x006e3e80 (constructor), FUN_006de240 (OnGameplayOpt), FUN_006de2c0 (OnAutopauseOpt), FUN_006e2df0 (OnFeedbackOpt), FUN_006e3d80 (OnGraphicsOpt), FUN_006e3e00 (OnSoundOpt), FUN_006de340 (SetDescription), FUN_006dff10 (HandleInputEvent)
        /// - Settings categories: Graphics, Sound, Gameplay, Feedback, Autopause
        /// - Graphics: Resolution, Texture Quality, Shadow Quality, VSync, Fullscreen
        /// - Sound: Master Volume, Music Volume, Effects Volume, Voice Volume
        /// - Gameplay: Mouse Sensitivity, Invert Mouse Y, Auto-save, Tooltips
        /// - Feedback: Tooltip options, combat feedback, etc.
        /// - Autopause: Autopause triggers (on enemy sighted, trap found, etc.)
        /// - Original implementation: Tabbed interface with Apply/Cancel buttons
        /// - Settings persistence: Saved to configuration files
        /// - Flow: Main Menu → Options Menu → (Apply/Cancel) → Main Menu
        /// </remarks>
        OptionsMenu,

        /// <summary>
        /// Gameplay options submenu - player is configuring gameplay-specific settings.
        /// </summary>
        /// <remarks>
        /// Gameplay Options Menu State:
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): CSWGuiOptionsMain::OnGameplayOpt @ 0x006de240
        /// - GUI Panel: "optionsgameplay" (gameplay options submenu)
        /// - Settings: Mouse Sensitivity, Invert Mouse Y, Auto-save, Tooltips, Difficulty
        /// - Original implementation: Submenu opened from main options menu via "BTN_GAMEPLAY" button
        /// - Flow: Options Menu → Gameplay Options Menu → (Apply/Cancel) → Options Menu
        /// </remarks>
        GameplayOptionsMenu,

        /// <summary>
        /// Graphics options submenu - player is configuring graphics-specific settings.
        /// </summary>
        /// <remarks>
        /// Graphics Options Menu State:
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): CSWGuiOptionsMain::OnGraphicsOpt @ 0x006e3d80
        /// - GUI Panel: "optionsgraphics" (graphics options submenu)
        /// - Settings: Resolution, Fullscreen, VSync, Texture Quality, Shadow Quality, Anisotropic Filtering, Anti-Aliasing
        /// - Original implementation: Submenu opened from main options menu via "BTN_GRAPHICS" button
        /// - Flow: Options Menu → Graphics Options Menu → (Apply/Cancel) → Options Menu
        /// </remarks>
        GraphicsOptionsMenu,

        /// <summary>
        /// Movies menu - player can view and replay cutscenes/cinematics.
        /// </summary>
        /// <remarks>
        /// Movies Menu State:
        /// - Based on swkotor.exe and swkotor2.exe movies menu system
        /// - Located via string references: "BTN_MOVIES" (movies button in main menu)
        /// - Movies are stored as BIK (Bink Video) files in the movies directory
        /// - Movie playback: Uses CExoMoviePlayerInternal (swkotor.exe/swkotor2.exe)
        /// - Function: FUN_00404c80 @ 0x00404c80 (main playback loop in swkotor.exe)
        /// - Function: FUN_004053e0 @ 0x004053e0 (movie initialization in swkotor.exe)
        /// - Movie file paths: "MOVIES:%s" format, ".\\movies" or "d:\\movies" directories
        /// - Original implementation: Movies menu lists available BIK files, player selects to play
        /// - Playback: Fullscreen, blocking until completion or cancellation
        /// </remarks>
        MoviesMenu
    }
}

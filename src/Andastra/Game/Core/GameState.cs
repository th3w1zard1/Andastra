namespace Andastra.Runtime.Game.Core
{
    /// <summary>
    /// Represents the current state of the game.
    /// </summary>
    /// <remarks>
    /// Game State Enum:
    /// - Based on swkotor2.exe game state management system
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
        CharacterCreation
    }
}

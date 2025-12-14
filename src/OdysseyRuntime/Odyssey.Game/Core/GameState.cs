namespace Odyssey.Game.Core
{
    /// <summary>
    /// Represents the current state of the game.
    /// </summary>
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
        Paused
    }
}

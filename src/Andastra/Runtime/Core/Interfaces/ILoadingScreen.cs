using System;

namespace Andastra.Runtime.Core.Interfaces
{
    /// <summary>
    /// Interface for loading screen display during module transitions.
    /// </summary>
    /// <remarks>
    /// Loading Screen Interface:
    /// - Based on swkotor.exe and swkotor2.exe loading screen system
    /// - Located via string references: "loadscreen_p" @ 0x007cbe40 (swkotor2.exe), "loadscreen" @ 0x00752db0 (swkotor.exe)
    /// - "LoadScreenID" @ 0x007bd54c (swkotor2.exe), "LoadScreenID" @ 0x00747880 (swkotor.exe)
    /// - "LBL_LOADING" @ 0x007cbe10 (swkotor2.exe), "Loading" @ 0x007c7e40 (swkotor2.exe)
    /// - "PB_PROGRESS" @ 0x007cb33c (progress bar), "LBL_HINT" (loading hints), "LBL_LOGO" (logo label)
    /// - Original implementation: FUN_006cff90 @ 0x006cff90 (swkotor2.exe) initializes loading screen GUI panel
    /// - Loading screen GUI: "loadscreen_p" GUI file contains panel with progress bar, hints, logo, and loading image
    /// - Loading screen image: Set via LoadScreenResRef from module IFO file (TPC format texture)
    /// - Loading screen display: Shown during module transitions, hidden after module load completes
    /// - Progress bar: Shows loading progress (0-100) during resource loading
    /// - Loading hints: Random hints from loadscreenhints.2da displayed during loading
    /// - Engine-specific implementations provide concrete functionality for their respective engines
    ///
    /// Based on reverse engineering of loading screen systems:
    /// - Odyssey (swkotor.exe, swkotor2.exe): GUI panel-based loading screen with progress bar and hints
    /// - Aurora (nwmain.exe): Similar GUI panel-based loading screen system
    /// - Eclipse (daorigins.exe, DragonAge2.exe): Advanced loading screen with animated backgrounds
    /// - Infinity (, ): Modern loading screen with cinematic transitions
    /// </remarks>
    public interface ILoadingScreen
    {
        /// <summary>
        /// Shows the loading screen with the specified image.
        /// </summary>
        /// <param name="imageResRef">Resource reference for the loading screen image (TPC format). If null or empty, uses default loading screen.</param>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_006cff90 @ 0x006cff90 initializes loading screen GUI
        /// - Loads "loadscreen_p" GUI panel
        /// - Sets loading screen image via LoadScreenResRef (TPC texture)
        /// - Displays progress bar, hints, and logo
        /// - Original implementation: Shows loading screen during module transitions
        /// </remarks>
        void Show(string imageResRef);

        /// <summary>
        /// Hides the loading screen.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Loading screen is hidden after module load completes
        /// - Hides "loadscreen_p" GUI panel
        /// - Clears loading screen state
        /// - Original implementation: Called after module transition completes
        /// </remarks>
        void Hide();

        /// <summary>
        /// Updates the loading screen progress bar.
        /// </summary>
        /// <param name="progress">Progress value (0-100).</param>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Progress bar updates during resource loading
        /// - "PB_PROGRESS" control shows loading progress
        /// - "Load Bar = %d" @ 0x007c760c (progress debug output)
        /// - Original implementation: Progress bar updates as resources are loaded
        /// </remarks>
        void SetProgress(int progress);

        /// <summary>
        /// Gets whether the loading screen is currently visible.
        /// </summary>
        bool IsVisible { get; }
    }
}


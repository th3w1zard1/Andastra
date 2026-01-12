using System;
using Andastra.Parsing.Resource.Formats.NCS.NCSDecomp.Node;

namespace Andastra.Parsing.Resource.Formats.NCS.NCSDecomp
{
    /// <summary>
    /// Interface for displaying registry-related dialogs to the user.
    /// Allows UI layers to provide custom dialog implementations while maintaining
    /// library independence from specific UI frameworks.
    /// </summary>
    public interface IRegistryDialogHandler
    {
        /// <summary>
        /// Shows a dialog with a message and a "don't show again" checkbox.
        /// </summary>
        /// <param name="title">The dialog title</param>
        /// <param name="message">The message to display</param>
        /// <param name="dontShowAgain">Output parameter indicating whether the user checked "don't show again"</param>
        /// <returns>True if the dialog was shown successfully, false otherwise</returns>
        bool ShowDialogWithDontShowAgain(string title, string message, out bool dontShowAgain);

        /// <summary>
        /// Shows a dialog with Yes/No buttons for user confirmation.
        /// Used for elevation prompts and other confirmation dialogs.
        /// </summary>
        /// <param name="title">The dialog title</param>
        /// <param name="message">The message to display</param>
        /// <param name="userChoice">Output parameter indicating whether the user clicked Yes (true) or No (false)</param>
        /// <returns>True if the dialog was shown successfully and user made a choice, false otherwise</returns>
        bool ShowYesNoDialog(string title, string message, out bool userChoice);
    }
}


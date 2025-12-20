using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using HolocronToolset.Config;
using HolocronToolset.Update;
using Newtonsoft.Json.Linq;

namespace HolocronToolset.Dialogs
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/update_process.py:24
    // Original: def run_progress_dialog(progress_queue: Queue, title: str = "Operation Progress") -> NoReturn:
    public static class UpdateProcess
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/update_process.py:41-140
        // Original: def start_update_process(release: GithubRelease, download_url: str) -> None:
        /// <summary>
        /// Starts the update process for the specified release and download URL.
        /// Downloads the update, extracts it, and applies it. The application will exit after the update is applied.
        /// </summary>
        /// <param name="release">The GitHub release object (typically a JObject from Newtonsoft.Json).</param>
        /// <param name="downloadUrl">The URL to download the update archive from.</param>
        /// <returns>A task that completes when the update process finishes (application will exit).</returns>
        public static async Task StartUpdateProcessAsync(object release, string downloadUrl)
        {
            if (release == null)
            {
                throw new ArgumentNullException(nameof(release));
            }
            if (string.IsNullOrEmpty(downloadUrl))
            {
                throw new ArgumentException("Download URL cannot be null or empty.", nameof(downloadUrl));
            }

            // Get the current main window for progress dialog positioning
            Window ownerWindow = GetMainWindow();

            // Create progress queue for communication between update process and progress dialog
            Queue<Dictionary<string, object>> progressQueue = new Queue<Dictionary<string, object>>();

            // Show progress dialog
            UpdateProgressDialog progressDialog = new UpdateProgressDialog(progressQueue);
            if (ownerWindow != null)
            {
                progressDialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            progressDialog.Show();

            // Extract release information
            JObject releaseObj = release as JObject;
            if (releaseObj == null)
            {
                throw new ArgumentException("Release must be a JObject", nameof(release));
            }

            string tagName = releaseObj["tag_name"]?.Value<string>() ?? "";
            JArray assets = releaseObj["assets"] as JArray;

            // Get expected archive filenames from release assets
            List<string> expectedArchiveFilenames = new List<string>();
            if (assets != null)
            {
                foreach (JObject asset in assets.OfType<JObject>())
                {
                    string assetName = asset["name"]?.Value<string>();
                    if (!string.IsNullOrEmpty(assetName))
                    {
                        expectedArchiveFilenames.Add(assetName);
                    }
                }
            }

            // Download progress hook
            void DownloadProgressHook(Dictionary<string, object> data)
            {
                lock (progressQueue)
                {
                    progressQueue.Enqueue(data);
                }
            }

            // Exit hook
            void ExitApp(bool killSelfHere)
            {
                try
                {
                    // Signal progress dialog to close
                    Dictionary<string, object> shutdownData = new Dictionary<string, object>
                    {
                        ["action"] = "shutdown",
                        ["data"] = new Dictionary<string, object>()
                    };
                    lock (progressQueue)
                    {
                        progressQueue.Enqueue(shutdownData);
                    }

                    // Wait a bit for the dialog to close
                    Thread.Sleep(500);

                    // Terminate threads (best effort)
                    TerminateThreads();

                    // Quit application
                    QuitApplication();

                    if (killSelfHere)
                    {
                        Environment.Exit(0);
                    }
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"Error in exit hook: {ex}");
                    if (killSelfHere)
                    {
                        Environment.Exit(1);
                    }
                }
            }

            // Create AppUpdate instance
            string currentVersion = ConfigInfo.CurrentVersion;
            string latestVersion = ConfigVersion.ToolsetTagToVersion(tagName);

            AppUpdate updater = new AppUpdate(
                new List<string> { downloadUrl },
                "HolocronToolset",
                currentVersion,
                latestVersion,
                new List<Action<Dictionary<string, object>>> { DownloadProgressHook },
                ExitApp,
                ConfigVersion.VersionToToolsetTag);

            // Override archive names getter if we have expected filenames
            if (expectedArchiveFilenames.Count > 0)
            {
                updater.GetArchiveNamesFunc = () => expectedArchiveFilenames;
            }

            try
            {
                // Update status: Downloading
                lock (progressQueue)
                {
                    progressQueue.Enqueue(new Dictionary<string, object>
                    {
                        ["action"] = "update_status",
                        ["text"] = "Downloading update..."
                    });
                }

                // Download the update
                bool downloadSuccess = updater.Download(background: false);
                if (!downloadSuccess)
                {
                    throw new Exception("Failed to download update");
                }

                // Update status: Restarting and Applying
                lock (progressQueue)
                {
                    progressQueue.Enqueue(new Dictionary<string, object>
                    {
                        ["action"] = "update_status",
                        ["text"] = "Restarting and Applying update..."
                    });
                }

                // Extract and restart
                updater.ExtractRestart();

                // Update status: Cleaning up
                lock (progressQueue)
                {
                    progressQueue.Enqueue(new Dictionary<string, object>
                    {
                        ["action"] = "update_status",
                        ["text"] = "Cleaning up..."
                    });
                }

                // Cleanup
                updater.Cleanup();
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error occurred while downloading/installing the toolset: {ex}");
                throw;
            }
            finally
            {
                ExitApp(killSelfHere: true);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/update_process.py:143-177
        // Original: def _terminate_qt_threads(log: RobustLogger):
        /// <summary>
        /// Terminates all non-essential background threads in preparation for application shutdown.
        /// This is a best-effort cleanup operation. Critical threads (UI, application lifecycle) are not terminated.
        /// </summary>
        private static void TerminateThreads()
        {
            // In .NET, we cannot forcibly terminate threads from another thread without using Thread.Abort(),
            // which is deprecated and unsafe. Instead, we rely on cancellation tokens and cooperative cancellation.
            // For update purposes, we rely on the fact that Environment.Exit() will terminate all threads.
            // This method is kept for API compatibility but performs no action, as proper thread management
            // should use CancellationToken throughout the application.
            System.Console.WriteLine("Thread termination handled by Environment.Exit() - cooperative cancellation preferred.");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/update_process.py:180-207
        // Original: def _quit_qt_application(log: RobustLogger):
        /// <summary>
        /// Quits the application properly, ensuring all windows are closed and resources are cleaned up.
        /// For update operations, the application will exit via Environment.Exit() called by AppUpdate.
        /// </summary>
        private static void QuitApplication()
        {
            // For update operations, AppUpdate.RunAsync() will call Environment.Exit() after launching the update script.
            // This method is kept for API compatibility. If needed for other shutdown scenarios, it could:
            // 1. Close all windows via Application.Current.Windows
            // 2. Stop the application event loop
            // However, for update operations, Environment.Exit() is appropriate as it ensures immediate termination.
            System.Console.WriteLine("Application exit handled by update process.");
        }

        /// <summary>
        /// Gets the main application window for use as the owner of modal dialogs.
        /// </summary>
        /// <returns>The main window, or null if not found.</returns>
        private static Window GetMainWindow()
        {
            // Try to find the main window from the application
            // Avalonia doesn't have Application.Windows, so we use a different approach
            if (Avalonia.Application.Current != null && Avalonia.Application.Current.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Return the main window if available
                if (desktop.MainWindow != null)
                {
                    return desktop.MainWindow;
                }

                // Otherwise, try to find the first visible window
                foreach (var window in desktop.Windows)
                {
                    if (window.IsVisible)
                    {
                        return window;
                    }
                }

                // Return the first window if any exist
                if (desktop.Windows.Count > 0)
                {
                    return desktop.Windows[0];
                }
            }

            return null;
        }
    }
}

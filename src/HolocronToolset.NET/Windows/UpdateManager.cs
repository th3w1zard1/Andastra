using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HolocronToolset.NET.Config;
using HolocronToolset.NET.Data;

namespace HolocronToolset.NET.Windows
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/update_manager.py:35
    // Original: class UpdateManager:
    public class UpdateManager
    {
        private GlobalSettings _settings;
        private bool _silent;
        private object _masterInfo; // Can be Dictionary<string, object> or Exception
        private object _edgeInfo; // Can be Dictionary<string, object> or Exception

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/update_manager.py:36-77
        // Original: def __init__(self, *, silent: bool = False):
        public UpdateManager(bool silent = false)
        {
            _settings = new GlobalSettings();
            _silent = silent;
            _masterInfo = null;
            _edgeInfo = null;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/update_manager.py:42-77
        // Original: def check_for_updates(self, *, silent: bool = False):
        public async Task CheckForUpdatesAsync(bool silent = false)
        {
            _silent = silent;

            try
            {
                if (_settings.UseBetaChannel)
                {
                    _edgeInfo = await ConfigUpdate.GetRemoteToolsetUpdateInfoAsync(useBetaChannel: true, silent: _silent);
                }

                _masterInfo = await ConfigUpdate.GetRemoteToolsetUpdateInfoAsync(useBetaChannel: false, silent: _silent);
                OnUpdateInfoFetched();
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error checking for updates: {ex}");
                if (!_silent)
                {
                    // Show error message - will be implemented with MessageBox.Avalonia
                }
            }
        }

        // Synchronous wrapper for compatibility
        public void CheckForUpdates(bool silent = false)
        {
            // Fire and forget - updates will be checked in background
            Task.Run(async () => await CheckForUpdatesAsync(silent));
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/update_manager.py:111-164
        // Original: def _on_update_info_fetched(self):
        private void OnUpdateInfoFetched()
        {
            if (_edgeInfo == null || _masterInfo == null)
            {
                return;
            }

            if (_masterInfo is Exception masterEx)
            {
                if (!_silent)
                {
                    // Show error message
                    System.Console.WriteLine($"Failed to fetch master update info: {masterEx}");
                }
                return;
            }

            if (_edgeInfo is Exception edgeEx)
            {
                if (!_silent)
                {
                    // Show error message
                    System.Console.WriteLine($"Failed to fetch edge update info: {edgeEx}");
                }
                return;
            }

            var masterDict = _masterInfo as Dictionary<string, object>;
            var edgeDict = _edgeInfo as Dictionary<string, object>;
            if (masterDict == null || (_settings.UseBetaChannel && edgeDict == null))
            {
                return;
            }

            var remoteInfo = _settings.UseBetaChannel ? edgeDict : masterDict;
            bool releaseVersionChecked = !_settings.UseBetaChannel;

            string greatestVersion = releaseVersionChecked
                ? remoteInfo.ContainsKey("toolsetLatestVersion") ? remoteInfo["toolsetLatestVersion"]?.ToString() ?? "" : ""
                : remoteInfo.ContainsKey("toolsetLatestBetaVersion") ? remoteInfo["toolsetLatestBetaVersion"]?.ToString() ?? "" : "";

            bool? isNewer = ConfigUpdate.IsRemoteVersionNewer(ConfigInfo.CurrentVersion, greatestVersion);
            bool isUpToDate = isNewer == false;

            DisplayVersionMessage(greatestVersion, isUpToDate, releaseVersionChecked);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/update_manager.py:187-220
        // Original: def _display_version_message(...):
        private void DisplayVersionMessage(string greatestVersion, bool isUpToDate, bool releaseVersionChecked)
        {
            if (isUpToDate)
            {
                if (_silent)
                {
                    return;
                }
                // Show "up to date" message - will be implemented with MessageBox.Avalonia
                System.Console.WriteLine($"You are running the latest version ({ConfigInfo.CurrentVersion})");
            }
            else
            {
                // Show update available message - will be implemented with MessageBox.Avalonia
                System.Console.WriteLine($"Update available: {greatestVersion} (current: {ConfigInfo.CurrentVersion})");
            }
        }
    }
}

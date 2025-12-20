using System;
using System.Collections.Generic;
using HolocronToolset.Data;

namespace HolocronToolset.NET
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/main_settings.py:16
    // Original: def setup_pre_init_settings():
    public static class MainSettings
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/main_settings.py:16-37
        // Original: def setup_pre_init_settings():
        public static void SetupPreInitSettings()
        {
            // Some application settings must be set before the app starts.
            // TODO: SIMPLIFIED - For now, this is a simplified version - full implementation will come with ApplicationSettings widget
            var settings = new Settings("Application");

            // Set environment variables from settings if needed
            // This will be expanded when ApplicationSettings widget is ported
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/main_settings.py:40-72
        // Original: def setup_post_init_settings():
        public static void SetupPostInitSettings()
        {
            // Set up post-initialization settings for the application.
            // This will be expanded when ApplicationSettings widget is ported
            var settings = new Settings("Application");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/main_settings.py:75-103
        // Original: def setup_toolset_default_env():
        /// <summary>
        /// Setup default environment variables for the toolset based on our recommendations.
        /// These can be configured in the toolset's Settings dialog.
        /// 
        /// Note: While Avalonia doesn't use Qt, we maintain 1:1 parity with PyKotor's PyQt implementation
        /// by setting these environment variables. They may be used by other tools or for compatibility.
        /// </summary>
        public static void SetupToolsetDefaultEnv()
        {
            // Force a real platform plugin. Upstream test harnesses default QT_QPA_PLATFORM
            // to "offscreen" (see Libraries/PyKotor/tests/conftest.py), which prevents the
            // ToolWindow from ever becoming exposed when run interactively. An empty string
            // lets Qt auto-select the native Windows plugin instead of the headless one.
            // Note: This is maintained for compatibility even though Avalonia doesn't use Qt.
            SetEnvironmentVariableIfNotSet("QT_QPA_PLATFORM", "");

            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                // On Windows, set Qt multimedia preferences
                // Note: These are maintained for compatibility even though Avalonia doesn't use Qt.
                SetEnvironmentVariableIfNotSet("QT_MULTIMEDIA_PREFERRED_PLUGINS", "windowsmediafoundation");
                SetEnvironmentVariableIfNotSet("QT_MEDIA_BACKEND", "windows");
                // On Windows, Qt uses the native Windows font system (GDI), not fontconfig
                // The font system initialization in setup_post_init_settings ensures system fonts are used
            }
            else
            {
                // On non-Windows platforms (Linux/macOS), fontconfig is the standard font system
                // Qt will use fontconfig automatically on these platforms
            }

            // Set debug plugin and logging settings based on debug mode and frozen state
            if (!MainInit.IsDebugMode() || MainInit.IsFrozen())
            {
                // Disable debug output when not in debug mode or when frozen
                SetEnvironmentVariableIfNotSet("QT_DEBUG_PLUGINS", "0");
                SetEnvironmentVariableIfNotSet("QT_LOGGING_RULES", "qt5ct.debug=false"); // Disable specific Qt debug output
            }
            else
            {
                // Enable debug output when in debug mode and not frozen
                SetEnvironmentVariableIfNotSet("QT_DEBUG_PLUGINS", "1");
                SetEnvironmentVariableIfNotSet("QT_LOGGING_RULES", "qt5ct.debug=true"); // Enable specific Qt debug output
            }
        }

        /// <summary>
        /// Sets an environment variable only if it's not already set.
        /// This preserves existing environment variable values, matching Python's os.environ.get behavior.
        /// </summary>
        /// <param name="name">The name of the environment variable.</param>
        /// <param name="defaultValue">The default value to set if the variable is not already set.</param>
        private static void SetEnvironmentVariableIfNotSet(string name, string defaultValue)
        {
            string existingValue = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(existingValue))
            {
                Environment.SetEnvironmentVariable(name, defaultValue);
            }
        }
    }
}

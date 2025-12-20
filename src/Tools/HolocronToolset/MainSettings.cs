using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using HolocronToolset.Data;

namespace HolocronToolset.NET
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/main_settings.py:16
    // Original: def setup_pre_init_settings():
    public static class MainSettings
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/main_settings.py:16-37
        // Original: def setup_pre_init_settings():
        /// <summary>
        /// Setup pre-initialization settings for the Holocron Toolset.
        /// 
        /// This function is called before the application window is created.
        /// It sets up environment variables from user settings, prioritizing existing
        /// environment variable values to allow system-level overrides.
        /// 
        /// Matching PyKotor: setup_pre_init_settings() in main_settings.py
        /// </summary>
        public static void SetupPreInitSettings()
        {
            // Some application settings must be set before the app starts.
            // These ones are accessible through the in-app settings window widget.
            var settings = new GlobalSettings();
            
            // Get environment variables from settings
            Dictionary<string, string> environmentVariables = settings.AppEnvVariables;
            
            // Set environment variables, prioritizing existing environment variable values
            // This matches Python's os.environ.get(key, value) behavior - only set if not already set
            foreach (var kvp in environmentVariables)
            {
                string key = kvp.Key;
                string value = kvp.Value;
                
                // Use Environment.GetEnvironmentVariable to check if already set
                // Only set if not already set (preserves existing env vars)
                string existingValue = Environment.GetEnvironmentVariable(key);
                if (string.IsNullOrEmpty(existingValue))
                {
                    Environment.SetEnvironmentVariable(key, value);
                }
            }
            
            // Note: In PyKotor, this also applies Qt attributes from REQUIRES_RESTART dict
            // Since we're using Avalonia instead of Qt, Qt-specific attributes don't apply.
            // However, we maintain the structure for potential future Avalonia equivalents.
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/main_settings.py:40-72
        // Original: def setup_post_init_settings():
        /// <summary>
        /// Set up post-initialization settings for the application.
        /// 
        /// This function performs the following tasks:
        /// 1. Retrieves the GlobalSettings instance and sets the global font.
        /// 2. Applies font settings that can be changed without restarting.
        /// 
        /// The function uses the GlobalSettings class to manage and apply various
        /// settings to the Avalonia Application instance.
        /// 
        /// Matching PyKotor: setup_post_init_settings() in main_settings.py
        /// </summary>
        public static void SetupPostInitSettings()
        {
            var settings = new GlobalSettings();
            
            // Apply font settings
            string fontString = settings.GlobalFont;
            if (!string.IsNullOrEmpty(fontString))
            {
                try
                {
                    // Parse font string (format: "Family|Size|Style|Weight")
                    var parts = fontString.Split('|');
                    if (parts.Length >= 2)
                    {
                        string family = parts[0].Trim();
                        if (double.TryParse(parts[1].Trim(), out double size))
                        {
                            var fontFamily = new FontFamily(family);
                            FontWeight weight = FontWeight.Normal;
                            FontStyle style = FontStyle.Normal;

                            // Parse weight if available
                            if (parts.Length >= 4 && int.TryParse(parts[3].Trim(), out int weightValue))
                            {
                                weight = weightValue >= 700 ? FontWeight.Bold : FontWeight.Normal;
                            }

                            // Parse style if available
                            if (parts.Length >= 3)
                            {
                                string styleStr = parts[2].Trim().ToLowerInvariant();
                                if (styleStr.Contains("italic"))
                                {
                                    style = FontStyle.Italic;
                                }
                            }

                            // Apply font to Application.Current.Resources
                            // In Avalonia, fonts are typically applied via styles/resources
                            if (Application.Current != null)
                            {
                                // Create a font resource that can be used throughout the application
                                var font = new Font(fontFamily, size, style, weight);
                                
                                // Set default font family and size in application resources
                                // This will be used as the default for all controls
                                if (!Application.Current.Resources.ContainsKey("DefaultFontFamily"))
                                {
                                    Application.Current.Resources.Add("DefaultFontFamily", fontFamily);
                                }
                                else
                                {
                                    Application.Current.Resources["DefaultFontFamily"] = fontFamily;
                                }
                                
                                if (!Application.Current.Resources.ContainsKey("DefaultFontSize"))
                                {
                                    Application.Current.Resources.Add("DefaultFontSize", size);
                                }
                                else
                                {
                                    Application.Current.Resources["DefaultFontSize"] = size;
                                }
                                
                                // Note: In PyKotor/Qt, QApplication.setFont() applies globally
                                // In Avalonia, fonts are typically applied via styles or per-control
                                // For now, we store the font preference in resources for use by styles
                            }
                        }
                    }
                }
                catch
                {
                    // If font parsing fails, use default font
                    // This matches PyKotor behavior where invalid font strings are ignored
                }
            }
            
            // Note: In PyKotor, this also applies Qt attributes (AA_*) and MISC_SETTINGS
            // Since we're using Avalonia instead of Qt, Qt-specific attributes don't apply.
            // However, we maintain the structure for potential future Avalonia equivalents.
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

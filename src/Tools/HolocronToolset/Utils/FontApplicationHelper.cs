using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using HolocronToolset.Data;

namespace HolocronToolset.Utils
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/application.py:69-77
    // Original: QApplication.setFont(font) - applies font globally to all Qt widgets
    // In Avalonia, fonts are applied via styles/resources to match Qt's global font behavior
    /// <summary>
    /// Helper class for applying global fonts to the Avalonia application.
    /// This matches PyKotor's QApplication.setFont() behavior by applying fonts via styles.
    /// </summary>
    public static class FontApplicationHelper
    {
        // Track the global font style so we can remove it later
        private static Style _globalFontStyle = null;
        // Matching PyKotor implementation: QApplication.setFont(font) applies globally
        // In Avalonia, we apply fonts via Application.Styles to achieve the same effect
        /// <summary>
        /// Applies a global font to the Avalonia application based on the font string from settings.
        /// This matches PyKotor's QApplication.setFont() behavior.
        /// </summary>
        /// <param name="fontString">Font string in format "Family|Size|Style|Weight" (e.g., "Arial|12|Normal|400")</param>
        public static void ApplyGlobalFont(string fontString)
        {
            if (Application.Current == null)
            {
                return;
            }

            try
            {
                // Parse font string (format: "Family|Size|Style|Weight")
                if (string.IsNullOrEmpty(fontString))
                {
                    // If font string is empty, remove custom font style (use default)
                    RemoveGlobalFontStyle();
                    return;
                }

                var parts = fontString.Split('|');
                if (parts.Length < 2)
                {
                    return;
                }

                string family = parts[0].Trim();
                if (!double.TryParse(parts[1].Trim(), out double size))
                {
                    return;
                }

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

                // Create FontFamily
                FontFamily fontFamily;
                try
                {
                    fontFamily = new FontFamily(family);
                }
                catch
                {
                    // If font family is invalid, use system default
                    fontFamily = FontFamily.Default;
                }

                // Apply font via styles
                ApplyGlobalFontStyle(fontFamily, size, weight, style);
            }
            catch
            {
                // If font parsing fails, use default font
                // This matches PyKotor behavior where invalid font strings are ignored
            }
        }

        // Matching PyKotor implementation: QApplication.setFont(font) applies globally
        // In Avalonia, we apply fonts via Application.Styles to achieve the same effect
        /// <summary>
        /// Applies a global font style to all controls in the application.
        /// This matches PyKotor's QApplication.setFont() behavior.
        /// </summary>
        /// <param name="fontFamily">The font family to apply.</param>
        /// <param name="fontSize">The font size to apply.</param>
        /// <param name="fontWeight">The font weight to apply.</param>
        /// <param name="fontStyle">The font style to apply.</param>
        private static void ApplyGlobalFontStyle(FontFamily fontFamily, double fontSize, FontWeight fontWeight, FontStyle fontStyle)
        {
            if (Application.Current == null)
            {
                return;
            }

            // Remove existing global font style if it exists
            RemoveGlobalFontStyle();

            // Create a style that applies to all controls
            // Matching PyKotor: QApplication.setFont() applies to all widgets
            // In Avalonia, we use a style with Selector="Control" to match all controls
            _globalFontStyle = new Style(x => x.OfType<Control>())
            {
                Setters =
                {
                    new Setter(Control.FontFamilyProperty, fontFamily),
                    new Setter(Control.FontSizeProperty, fontSize),
                    new Setter(Control.FontWeightProperty, fontWeight),
                    new Setter(Control.FontStyleProperty, fontStyle)
                }
            };

            // Add the style to Application.Styles
            // This will apply to all controls in the application
            Application.Current.Styles.Add(_globalFontStyle);

            // Also store in resources for reference
            Application.Current.Resources["GlobalFontFamily"] = fontFamily;
            Application.Current.Resources["GlobalFontSize"] = fontSize;
            Application.Current.Resources["GlobalFontWeight"] = fontWeight;
            Application.Current.Resources["GlobalFontStyle"] = fontStyle;
        }

        /// <summary>
        /// Removes the global font style from the application, reverting to default fonts.
        /// </summary>
        private static void RemoveGlobalFontStyle()
        {
            if (Application.Current == null)
            {
                return;
            }

            // Remove global font resources
            Application.Current.Resources.Remove("GlobalFontFamily");
            Application.Current.Resources.Remove("GlobalFontSize");
            Application.Current.Resources.Remove("GlobalFontWeight");
            Application.Current.Resources.Remove("GlobalFontStyle");

            // Remove the tracked global font style if it exists
            if (_globalFontStyle != null)
            {
                Application.Current.Styles.Remove(_globalFontStyle);
                _globalFontStyle = null;
            }
        }

        /// <summary>
        /// Applies the global font from GlobalSettings to the application.
        /// This should be called on application startup and when the font is changed.
        /// </summary>
        public static void ApplyGlobalFontFromSettings()
        {
            var settings = GlobalSettings.Instance;
            string fontString = settings.GlobalFont;
            ApplyGlobalFont(fontString);
        }
    }
}


using System;
using System.Collections.Generic;
using System.Linq;

namespace HolocronToolset.Data
{
    /// <summary>
    /// Represents a Qt environment variable definition.
    /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/env_vars.py:13-19
    /// </summary>
    public class EnvVar
    {
        public string Name { get; }
        public string Description { get; }
        public string Default { get; }
        public string PossibleValues { get; }
        public string DocLink { get; }
        public string Group { get; }

        public EnvVar(string name, string description, string defaultValue, string possibleValues, string docLink, string group)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            Default = defaultValue ?? throw new ArgumentNullException(nameof(defaultValue));
            PossibleValues = possibleValues ?? throw new ArgumentNullException(nameof(possibleValues));
            DocLink = docLink ?? throw new ArgumentNullException(nameof(docLink));
            Group = group ?? throw new ArgumentNullException(nameof(group));
        }

        /// <summary>
        /// List of all supported Qt environment variables.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/env_vars.py:22-181
        /// </summary>
        public static readonly List<EnvVar> ENV_VARS = new List<EnvVar>
        {
            new EnvVar("QT_3D_OPENGL_CONTEXT_PROFILE", "Specifies the OpenGL context profile for 3D.", "core", "core, compatibility", "https://doc.qt.io/qt-5/qtopengl.html", "3D"),
            new EnvVar("QT_AUTO_SCREEN_SCALE_FACTOR", "Enables automatic scaling of screen content.", "0", "0, 1", "https://doc.qt.io/qt-5/highdpi.html", "High DPI"),
            new EnvVar("QT_DEBUG_CONSOLE", "Enables the debug console for logging.", "0", "0, 1", "https://doc.qt.io/qt-5/qdebug.html", "Debugging"),
            new EnvVar("QT_DEBUG_PLUGINS", "Enables debugging for plugins.", "0", "0, 1", "https://doc.qt.io/qt-5/plugins-howto.html", "Debugging"),
            new EnvVar("QT_DEPRECATION_WARNINGS", "Enables or disables deprecation warnings.", "1", "0, 1", "https://doc.qt.io/qt-5/qglobal.html#Q_DEPRECATED", "General"),
            new EnvVar("QT_EGLFS_HEIGHT", "Specifies the height for EGLFS.", "Your screen height", "Any positive integer", "https://doc.qt.io/qt-5/embedded-linux.html", "EGLFS"),
            new EnvVar("QT_EGLFS_PHYSICAL_HEIGHT", "Sets the physical height for EGLFS.", "Screen physical height", "Any positive integer", "https://doc.qt.io/qt-5/embedded-linux.html", "EGLFS"),
            new EnvVar("QT_EGLFS_PHYSICAL_WIDTH", "Sets the physical width for EGLFS.", "Screen physical width", "Any positive integer", "https://doc.qt.io/qt-5/embedded-linux.html", "EGLFS"),
            new EnvVar("QT_EGLFS_WIDTH", "Specifies the width for EGLFS.", "Screen width", "Any positive integer", "https://doc.qt.io/qt-5/embedded-linux.html", "EGLFS"),
            new EnvVar("QT_ENABLE_HIGHDPI_SCALING", "Enables automatic high-DPI scaling.", "0", "0, 1", "https://doc.qt.io/qt-5/highdpi.html", "High DPI"),
            new EnvVar("QT_FB_FORCE_FULLSCREEN", "Forces fullscreen mode in framebuffer.", "0", "0, 1", "https://doc.qt.io/qt-5/embedded-linux.html", "Framebuffer"),
            new EnvVar("QT_FONT_DPI", "Overrides the default font DPI.", "96", "Any positive integer", "https://doc.qt.io/qt-5/highdpi.html", "High DPI"),
            new EnvVar("QT_FORCE_CLEANUP_ON_EXIT", "Forces cleanup on exit.", "0", "0, 1", "https://doc.qt.io/qt-5/qtglobal.html", "General"),
            new EnvVar("QT_FORCE_PLUGIN_FILE_LOADING", "Forces loading plugins from files.", "0", "0, 1", "https://doc.qt.io/qt-5/plugins-howto.html", "Plugins"),
            new EnvVar("QT_FORCE_TLS1_2", "Forces the use of TLS 1.2.", "0", "0, 1", "https://doc.qt.io/qt-5/qtnetwork.html", "Network"),
            new EnvVar("QT_FORCE_XRUNTIME", "Forces the use of XRUNTIME.", "0", "0, 1", "https://doc.qt.io/qt-5/x11extras.html", "X11"),
            new EnvVar("QT_GSTREAMER_PLUGIN_PATH", "Specifies the path for GStreamer plugins.", "System path", "Any valid directory path", "https://doc.qt.io/qt-5/qtmultimedia.html", "Multimedia"),
            new EnvVar("QT_GRAPHICSSYSTEM", "Specifies the graphics system to use.", "opengl", "opengl, raster", "https://doc.qt.io/qt-5/qtsystems.html", "Graphics"),
            new EnvVar("QT_IM_MODULE", "Specifies the input method module to use.", "xim", "xim, ibus, fcitx", "https://doc.qt.io/qt-5/inputmethods.html", "Input"),
            new EnvVar("QT_LARGEFILE_SUPPORT", "Enables support for large files.", "0", "0, 1", "https://doc.qt.io/qt-5/qtglobal.html", "Filesystem"),
            new EnvVar("QT_LINUX_ACCESSIBILITY_ALWAYS_ON", "Forces accessibility support on Linux.", "0", "0, 1", "https://doc.qt.io/qt-5/qtglobal.html", "Accessibility"),
            new EnvVar("QT_LOGGING_CONF", "Specifies the logging configuration file.", "None", "Any valid file path", "https://doc.qt.io/qt-5/qloggingcategory.html", "Logging"),
            new EnvVar("QT_LOGGING_DEPLOY", "Enables logging for deployment.", "0", "0, 1", "https://doc.qt.io/qt-5/qloggingcategory.html", "Logging"),
            new EnvVar("QT_LOGGING_RULES", "Configures logging rules for categories.", "None", "Category filter string", "https://doc.qt.io/qt-5/qloggingcategory.html", "Logging"),
            new EnvVar("QT_LOGGING_TO_CONSOLE", "Enables logging to the console.", "0", "0, 1", "https://doc.qt.io/qt-5/qloggingcategory.html", "Logging"),
            new EnvVar("QT_NETWORK_PROXY", "Configures network proxy settings.", "None", "proxy settings", "https://doc.qt.io/qt-5/qnetworkproxy.html", "Network"),
            new EnvVar("QT_NO_CHILD_STYLE", "Disables child widget styling.", "0", "0, 1", "https://doc.qt.io/qt-5/stylesheet.html", "Styling"),
            new EnvVar("QT_NO_COLOR_STYLE", "Disables color styling.", "0", "0, 1", "https://doc.qt.io/qt-5/stylesheet.html", "Styling"),
            new EnvVar("QT_NO_FONT_FALLBACK", "Disables font fallback.", "0", "0, 1", "https://doc.qt.io/qt-5/qfont.html", "Fonts"),
            new EnvVar("QT_NO_LINKING", "Disables linking checks.", "0", "0, 1", "https://doc.qt.io/qt-5/qtglobal.html", "General"),
            new EnvVar("QT_NO_SHORTCUT_OVERRIDE", "Disables shortcut overrides.", "0", "0, 1", "https://doc.qt.io/qt-5/qshortcut.html", "Input"),
            new EnvVar("QT_NO_VENDOR_VARIABLES", "Disables vendor-specific environment variables.", "0", "0, 1", "https://doc.qt.io/qt-5/qtglobal.html", "General"),
            new EnvVar("QT_OPENGL", "Sets the type of OpenGL implementation.", "desktop", "desktop, es2, software", "https://doc.qt.io/qt-5/qtopengl.html", "Graphics"),
            new EnvVar("QT_OPENGL_DEBUG", "Enables OpenGL debugging.", "0", "0, 1", "https://doc.qt.io/qt-5/qtopengl.html", "Graphics"),
            new EnvVar("QT_PLUGIN_PATH", "Specifies the search path for plugins.", "System path", "Any valid directory path", "https://doc.qt.io/qt-5/plugins-howto.html", "Plugins"),
            new EnvVar("QT_QPA_EGLFS_DISABLE_INPUT", "Disables input on EGLFS.", "0", "0, 1", "https://doc.qt.io/qt-5/embedded-linux.html", "EGLFS"),
            new EnvVar("QT_QPA_EGLFS_FORCEVSYNC", "Forces VSync on EGLFS.", "1", "0, 1", "https://doc.qt.io/qt-5/embedded-linux.html", "EGLFS"),
            new EnvVar("QT_QPA_EGLFS_INTEGRATION", "Specifies the EGLFS backend.", "None", "eglfs, kms, mali", "https://doc.qt.io/qt-5/embedded-linux.html", "EGLFS"),
            new EnvVar("QT_QPA_EGLFS_KMS_CONFIG", "Specifies KMS configuration for EGLFS.", "None", "Any valid file path", "https://doc.qt.io/qt-5/embedded-linux.html", "EGLFS"),
            new EnvVar("QT_QPA_EGLFS_NO_VSYNC", "Disables VSync on EGLFS.", "0", "0, 1", "https://doc.qt.io/qt-5/embedded-linux.html", "EGLFS"),
            new EnvVar("QT_QPA_FB_DRM", "Enables DRM support in the framebuffer.", "0", "0, 1", "https://doc.qt.io/qt-5/embedded-linux.html", "Framebuffer"),
            new EnvVar("QT_QPA_FB_FORCE_DOUBLE_BUFFER", "Forces double buffering in framebuffer.", "0", "0, 1", "https://doc.qt.io/qt-5/embedded-linux.html", "Framebuffer"),
            new EnvVar("QT_QPA_FB_FORCE_FULLSCREEN", "Forces fullscreen mode in framebuffer.", "0", "0, 1", "https://doc.qt.io/qt-5/embedded-linux.html", "Framebuffer"),
            new EnvVar("QT_QPA_FB_NO_LIBINPUT", "Disables libinput support in framebuffer.", "0", "0, 1", "https://doc.qt.io/qt-5/embedded-linux.html", "Framebuffer"),
            new EnvVar("QT_QPA_FB_NO_TSLIB", "Disables tslib support in framebuffer.", "0", "0, 1", "https://doc.qt.io/qt-5/embedded-linux.html", "Framebuffer"),
            new EnvVar("QT_QPA_FB_TSLIB", "Enables tslib support on Linux/FB.", "0", "0, 1", "https://doc.qt.io/qt-5/embedded-linux.html", "Framebuffer"),
            new EnvVar("QT_QPA_FONTDIR", "Specifies the font directory.", "System path", "Any valid directory path", "https://doc.qt.io/qt-5/qpa.html", "Fonts"),
            new EnvVar("QT_QPA_FORCE_NATIVE_WINDOWS", "Forces native windows in QPA.", "0", "0, 1", "https://doc.qt.io/qt-5/qpa.html", "QPA"),
            new EnvVar("QT_QPA_GENERIC_PLUGINS", "Specifies generic plugins to load.", "None", "Comma-separated list of plugins", "https://doc.qt.io/qt-5/qpa.html", "QPA"),
            new EnvVar("QT_QPA_MULTIMONITOR", "Enables multi-monitor support.", "0", "0, 1", "https://doc.qt.io/qt-5/qpa.html", "QPA"),
            new EnvVar("QT_QPA_NO_SYSTEM_TRAY", "Disables the system tray.", "0", "0, 1", "https://doc.qt.io/qt-5/qpa.html", "QPA"),
            new EnvVar("QT_QPA_OFFSCREEN", "Enables offscreen rendering.", "0", "0, 1", "https://doc.qt.io/qt-5/qpa.html", "QPA"),
            new EnvVar("QT_QPA_PLATFORM", "Specifies the platform plugin to load.", "xcb", "xcb, wayland, eglfs", "https://doc.qt.io/qt-5/qpa.html", "QPA"),
            new EnvVar("QT_QPA_PLATFORM_PLUGIN_PATH", "Specifies the platform plugin search path.", "System path", "Any valid directory path", "https://doc.qt.io/qt-5/qpa.html", "QPA"),
            new EnvVar("QT_QPA_PLATFORMTHEME", "Specifies the platform theme plugin to use.", "None", "Any valid theme name", "https://doc.qt.io/qt-5/qpa.html", "QPA"),
            new EnvVar("QT_QPA_RHI_BACKEND", "Specifies the RHI backend to use.", "None", "vulkan, metal, d3d11, opengl", "https://doc.qt.io/qt-5/qpa.html", "QPA"),
            new EnvVar("QT_QPA_SCREEN_SCALE_FACTORS", "Specifies screen scale factors.", "None", "Comma-separated list of scale factors", "https://doc.qt.io/qt-5/qpa.html", "QPA"),
            new EnvVar("QT_QPA_USE_RGBA", "Forces the use of RGBA for platform surfaces.", "0", "0, 1", "https://doc.qt.io/qt-5/qpa.html", "QPA"),
            new EnvVar("QT_QPA_XCB_NATIVE_PAINTING", "Enables native painting on XCB.", "0", "0, 1", "https://doc.qt.io/qt-5/qpa.html", "QPA"),
            new EnvVar("QT_QPA_XCB_TABLET_LEGACY", "Enables legacy tablet support on XCB.", "0", "0, 1", "https://doc.qt.io/qt-5/qpa.html", "QPA"),
            new EnvVar("QT_QPA_XCB_GL_INTEGRATION", "Selects the XCB OpenGL integration plugin.", "None", "xcb-glx, xcb-egl", "https://doc.qt.io/qt-5/qpa.html", "QPA"),
            new EnvVar("QT_QPA_XCB_NO_MITSHM", "Disables MIT-SHM on X11.", "0", "0, 1", "https://doc.qt.io/qt-5/qpa.html", "QPA"),
            new EnvVar("QT_QPA_XCB_RENDERER", "Specifies the renderer to use on XCB.", "None", "Any valid renderer name", "https://doc.qt.io/qt-5/qpa.html", "QPA"),
            new EnvVar("QT_QUICK_CONTROLS_1_STYLE", "Specifies the style for Quick Controls 1.", "None", "Any valid style name", "https://doc.qt.io/qt-5/qtquickcontrols-index.html", "Quick Controls"),
            new EnvVar("QT_QUICK_CONTROLS_CONF", "Specifies the configuration file for Quick Controls.", "None", "Any valid file path", "https://doc.qt.io/qt-5/qtquickcontrols-index.html", "Quick Controls"),
            new EnvVar("QT_QUICK_CONTROLS_FALLBACK_STYLE", "Sets the fallback style for Quick Controls.", "None", "Any valid style name", "https://doc.qt.io/qt-5/qtquickcontrols-index.html", "Quick Controls"),
            new EnvVar("QT_QUICK_CONTROLS_HOVER_ENABLED", "Enables hover events for Quick Controls.", "0", "0, 1", "https://doc.qt.io/qt-5/qtquickcontrols-index.html", "Quick Controls"),
            new EnvVar("QT_QUICK_CONTROLS_MATERIAL_THEME", "Sets the Material theme for Quick Controls.", "None", "Any valid theme name", "https://doc.qt.io/qt-5/qtquickcontrols-index.html", "Quick Controls"),
            new EnvVar("QT_QUICK_CONTROLS_STYLE", "Specifies the style for Quick Controls.", "None", "Any valid style name", "https://doc.qt.io/qt-5/qtquickcontrols-index.html", "Quick Controls"),
            new EnvVar("QT_SCALE_FACTOR", "Sets the scale factor for the application.", "1.0", "Any positive floating-point value", "https://doc.qt.io/qt-5/highdpi.html", "High DPI"),
            new EnvVar("QT_SSL_CERTIFICATES", "Specifies the path to SSL certificates.", "System path", "Any valid directory path", "https://doc.qt.io/qt-5/qtnetwork.html", "Network"),
            new EnvVar("QT_SSL_NO_WARN_CIPHERS", "Disables SSL cipher warnings.", "0", "0, 1", "https://doc.qt.io/qt-5/qtnetwork.html", "Network"),
            new EnvVar("QT_STYLE_OVERRIDE", "Forces the use of a specific style.", "None", "Any valid style name", "https://doc.qt.io/qt-5/qstyle.html", "Styling"),
            new EnvVar("QT_USE_NATIVE_WINDOWS", "Enables or disables native windows.", "1", "0, 1", "https://doc.qt.io/qt-5/qpa.html", "QPA"),
            new EnvVar("QT_USE_QIODEVICE_OPEN_MODE", "Uses the open mode of QIODevice.", "0", "0, 1", "https://doc.qt.io/qt-5/qiodevice.html", "General"),
            new EnvVar("QT_USE_QSTRINGBUILDER", "Enables the use of QStringBuilder.", "1", "0, 1", "https://doc.qt.io/qt-5/qstringbuilder.html", "General"),
            new EnvVar("QT_VIRTUALKEYBOARD_DISABLE_TIMESTAMPS", "Disables timestamps for virtual keyboard.", "0", "0, 1", "https://doc.qt.io/qt-5/qtvirtualkeyboard.html", "Virtual Keyboard"),
            new EnvVar("QT_VIRTUALKEYBOARD_LAYOUT_PATH", "Specifies the path for virtual keyboard layouts.", "None", "Any valid directory path", "https://doc.qt.io/qt-5/qtvirtualkeyboard.html", "Virtual Keyboard"),
            new EnvVar("QT_VULKAN_LAYER_PATH", "Specifies the path to Vulkan layers.", "None", "Any valid directory path", "https://doc.qt.io/qt-5/qvulkaninstance.html", "Vulkan"),
            new EnvVar("QT_WEBENGINE_CHROMIUM_FLAGS", "Passes flags to the embedded Chromium.", "None", "Any valid Chromium flag", "https://doc.qt.io/qt-5/qtwebengine.html", "WebEngine"),
            new EnvVar("QT_WEBENGINE_DISABLE_GPU", "Disables GPU acceleration in WebEngine.", "0", "0, 1", "https://doc.qt.io/qt-5/qtwebengine.html", "WebEngine"),
            new EnvVar("QT_WIDGETS_BYPASS_NATIVE_EVENTS", "Bypasses native events in Qt Widgets.", "0", "0, 1", "https://doc.qt.io/qt-5/qtwidgets-index.html", "Widgets"),
            new EnvVar("QT_WIDGETS_DISABLE_NATIVE_WIDGETS", "Disables native widgets support.", "0", "0, 1", "https://doc.qt.io/qt-5/qtwidgets-index.html", "Widgets"),
            new EnvVar("QT_WIDGETS_FATAL_WARNINGS", "Treats warnings as fatal in Qt Widgets.", "0", "0, 1", "https://doc.qt.io/qt-5/qtwidgets-index.html", "Widgets"),
            new EnvVar("QT_XCB_FORCE_SOFTWARE_VSYNC", "Forces software VSync on XCB.", "0", "0, 1", "https://doc.qt.io/qt-5/qpa.html", "XCB"),
            new EnvVar("QT_XCB_GL_INTEGRATION", "Selects the XCB OpenGL integration plugin.", "xcb-glx", "xcb-glx, xcb-egl", "https://doc.qt.io/qt-5/qpa.html", "XCB"),
            new EnvVar("QT_XCB_RENDERER", "Specifies the renderer to use on XCB.", "None", "Any valid renderer name", "https://doc.qt.io/qt-5/qpa.html", "XCB"),
            new EnvVar("QT_XCB_TABLET_LEGACY", "Enables legacy tablet support on XCB.", "0", "0, 1", "https://doc.qt.io/qt-5/qpa.html", "XCB"),
            new EnvVar("QT_X11_NO_MITSHM", "Disables MIT-SHM on X11.", "0", "0, 1", "https://doc.qt.io/qt-5/x11extras.html", "X11"),
            new EnvVar("QTWEBENGINE_CHROMIUM_FLAGS", "Passes flags to the embedded Chromium.", "None", "Any valid Chromium flag", "https://doc.qt.io/qt-5/qtwebengine.html", "WebEngine"),
            new EnvVar("QTWEBENGINE_DISABLE_SANDBOX", "Disables the Chromium sandbox.", "0", "0, 1", "https://doc.qt.io/qt-5/qtwebengine.html", "WebEngine"),
            new EnvVar("QTWEBENGINE_PROCESS_PATH", "Specifies the WebEngine process path.", "None", "Any valid file path", "https://doc.qt.io/qt-5/qtwebengine.html", "WebEngine"),
            new EnvVar("QTWEBENGINE_PROFILE", "Specifies the WebEngine profile to use.", "None", "Any valid profile name", "https://doc.qt.io/qt-5/qtwebengine.html", "WebEngine"),
            new EnvVar("QT_ENABLE_GLYPH_CACHE", "Enables the glyph cache in the raster engine.", "1", "0, 1", "https://doc.qt.io/qt-5/qpainter.html", "Graphics"),
            new EnvVar("QT_LOGGING_ENABLED", "Enables logging globally for the application.", "1", "0, 1", "https://doc.qt.io/qt-5/qloggingcategory.html", "Logging"),
            new EnvVar("QT_NO_DIRECT3D", "Disables Direct3D support.", "0", "0, 1", "https://doc.qt.io/qt-5/windows.html", "Graphics"),
            new EnvVar("QT_OPACITY_THRESHOLD", "Sets the opacity threshold for translucent windows.", "0.5", "Any positive floating-point value", "https://doc.qt.io/qt-5/qwidget.html", "Styling"),
            new EnvVar("QT_SCALE_FACTOR_ROUNDING_POLICY", "Specifies the rounding policy for scaling factors.", "Round", "Round, Ceil, Floor", "https://doc.qt.io/qt-5/qhighdpiscaling.html", "High DPI"),
            new EnvVar("QT_WIDGETS_TEXTURE_SIZE", "Specifies the texture size for widget rendering.", "None", "Any positive integer", "https://doc.qt.io/qt-5/qtwidgets-index.html", "Widgets"),
            new EnvVar("QT_WINRT_FORCE", "Forces the use of WinRT platform integration.", "0", "0, 1", "https://doc.qt.io/qt-5/winrt.html", "Windows"),
            new EnvVar("QT_WINRT_HIDPI_SCALING", "Enables HiDPI scaling on WinRT.", "1", "0, 1", "https://doc.qt.io/qt-5/winrt.html", "Windows"),
            new EnvVar("QT_ANIMATION_DURATION_FACTOR", "Scales the duration of animations globally.", "1.0", "Any positive floating-point value", "https://doc.qt.io/qt-5/qpropertyanimation.html", "Styling"),
            new EnvVar("QT_AUTO_SCREEN_ROTATION", "Enables automatic screen rotation based on sensor data.", "0", "0, 1", "https://doc.qt.io/qt-5/qscreen.html", "General"),
            new EnvVar("QT_COMPACT_CONTROLS", "Forces the use of compact controls for UI elements.", "0", "0, 1", "https://doc.qt.io/qt-5/qtquickcontrols-index.html", "Quick Controls"),
            new EnvVar("QT_CUSTOM_CURSOR_THEME", "Specifies a custom theme for mouse cursors.", "None", "Any valid theme name", "https://doc.qt.io/qt-5/qcursor.html", "Styling"),
            new EnvVar("QT_ENABLE_EXTENDED_TYPES", "Enables support for extended data types in QVariant.", "1", "0, 1", "https://doc.qt.io/qt-5/qvariant.html", "General"),
            new EnvVar("QT_ENABLE_TRANSLUCENT_WINDOWS", "Enables translucent window support.", "0", "0, 1", "https://doc.qt.io/qt-5/qwidget.html", "Styling"),
            new EnvVar("QT_FONT_CACHE_SIZE", "Sets the size of the font cache.", "2048", "Any positive integer", "https://doc.qt.io/qt-5/qfont.html", "Fonts"),
            new EnvVar("QT_GRAPHICSSYSTEM_DEBUG", "Enables debugging for the graphics system.", "0", "0, 1", "https://doc.qt.io/qt-5/qtopengl.html", "Graphics"),
            new EnvVar("QT_HOTKEYS_DISABLED", "Disables global hotkeys in the application.", "0", "0, 1", "https://doc.qt.io/qt-5/qshortcut.html", "Input"),
            new EnvVar("QT_IGNORE_DEPRECATED_WARNINGS", "Ignores warnings about deprecated functions.", "0", "0, 1", "https://doc.qt.io/qt-5/qglobal.html#Q_DECL_DEPRECATED", "Debugging"),
            new EnvVar("QT_IMAGE_CACHE_SIZE", "Sets the size of the image cache.", "1024", "Any positive integer", "https://doc.qt.io/qt-5/qimage.html", "Graphics"),
            new EnvVar("QT_IM_SWITCHER_VISIBLE", "Forces the visibility of the input method switcher.", "1", "0, 1", "https://doc.qt.io/qt-5/qinputmethod.html", "Input"),
            new EnvVar("QT_INVERT_COLORS", "Inverts colors globally in the application.", "0", "0, 1", "https://doc.qt.io/qt-5/qcolor.html", "Styling"),
            new EnvVar("QT_KEEP_SYSTEM_IDLE", "Prevents the system from entering idle state while the application is running.", "0", "0, 1", "https://doc.qt.io/qt-5/qsystemtrayicon.html", "General"),
            new EnvVar("QT_KEYBOARD_LAYOUT", "Specifies the keyboard layout to use.", "System layout", "Any valid layout identifier", "https://doc.qt.io/qt-5/qkeysequence.html", "Input"),
            new EnvVar("QT_MAXIMUM_TEXTURE_SIZE", "Sets the maximum texture size for rendering.", "None", "Any positive integer", "https://doc.qt.io/qt-5/qopenglwidget.html", "Graphics"),
            new EnvVar("QT_MINIMUM_WIDGET_SIZE", "Sets the minimum size for all widgets.", "None", "Any valid size", "https://doc.qt.io/qt-5/qwidget.html", "Widgets"),
            new EnvVar("QT_MOUSE_ACCELERATION", "Enables or disables mouse acceleration.", "1", "0, 1", "https://doc.qt.io/qt-5/qcursor.html", "Input"),
            new EnvVar("QT_NO_ANIMATIONS", "Disables all animations in the application.", "0", "0, 1", "https://doc.qt.io/qt-5/qpropertyanimation.html", "Styling"),
            new EnvVar("QT_NO_SCREEN_SAVER", "Prevents the screen saver from being activated.", "0", "0, 1", "https://doc.qt.io/qt-5/qscreen.html", "General"),
            new EnvVar("QT_NO_SPLASH_SCREEN", "Disables the splash screen.", "0", "0, 1", "https://doc.qt.io/qt-5/qsplashscreen.html", "General"),
            new EnvVar("QT_NO_UNDERSCORE_ACCEL", "Disables underscore shortcuts in menus.", "0", "0, 1", "https://doc.qt.io/qt-5/qmenu.html", "Input"),
            new EnvVar("QT_OPENGL_FORCE_MSAA", "Forces multisample anti-aliasing (MSAA) in OpenGL.", "0", "0, 1", "https://doc.qt.io/qt-5/qopenglwidget.html", "Graphics"),
            new EnvVar("QT_OPACITY_ROUNDED", "Enables rounded opacity values for widgets.", "1", "0, 1", "https://doc.qt.io/qt-5/qwidget.html", "Styling"),
            new EnvVar("QT_OVERRIDE_WINDOW_STATE", "Overrides the default window state behavior.", "0", "0, 1", "https://doc.qt.io/qt-5/qwindow.html", "General"),
            new EnvVar("QT_POINTER_EVENTS_ENABLED", "Enables pointer events globally in the application.", "1", "0, 1", "https://doc.qt.io/qt-5/qevent.html", "Input"),
            new EnvVar("QT_PRELOAD_THEMES", "Preloads themes to speed up theme switching.", "0", "0, 1", "https://doc.qt.io/qt-5/qstyle.html", "Styling"),
            new EnvVar("QT_PRINT_DEBUG_INFO", "Prints debug information to the console.", "0", "0, 1", "https://doc.qt.io/qt-5/qdebug.html", "Debugging"),
            new EnvVar("QT_QUICK_ACCELERATED_RENDERING", "Forces accelerated rendering in Qt Quick.", "1", "0, 1", "https://doc.qt.io/qt-5/qtquick-index.html", "Quick Controls"),
            new EnvVar("QT_QUICK_DISABLE_SHADERS", "Disables shader programs in Qt Quick.", "0", "0, 1", "https://doc.qt.io/qt-5/qtquick-index.html", "Quick Controls"),
            new EnvVar("QT_QUICK_FORCE_GL_TEXTURE", "Forces the use of GL textures in Qt Quick.", "0", "0, 1", "https://doc.qt.io/qt-5/qtquick-index.html", "Quick Controls"),
            new EnvVar("QT_QUICK_FPS", "Enables displaying frames per second (FPS) in Qt Quick applications.", "0", "0, 1", "https://doc.qt.io/qt-5/qtquick-index.html", "Quick Controls"),
            new EnvVar("QT_QUICK_PARTICLE_SYSTEM", "Enables the particle system in Qt Quick.", "1", "0, 1", "https://doc.qt.io/qt-5/qtquick-particles.html", "Quick Controls"),
            new EnvVar("QT_QUICK_TOUCH_HANDLERS", "Enables touch handlers in Qt Quick.", "1", "0, 1", "https://doc.qt.io/qt-5/qquickitem.html", "Quick Controls"),
            new EnvVar("QT_QWS_DISPLAY", "Specifies the display to use in Qt for Embedded Linux.", "/dev/fb0", "Any valid framebuffer device", "https://doc.qt.io/archives/qt-4.8/qws.html", "Embedded Linux"),
            new EnvVar("QT_QWS_MOUSE_PROTO", "Specifies the mouse protocol for Qt for Embedded Linux.", "Auto", "Auto, linuxinput, qvfbmouse", "https://doc.qt.io/archives/qt-4.8/qws.html", "Embedded Linux"),
            new EnvVar("QT_QWS_SIZE", "Sets the size of the display in Qt for Embedded Linux.", "None", "Any valid size", "https://doc.qt.io/archives/qt-4.8/qws.html", "Embedded Linux"),
            new EnvVar("QT_QWS_KEYBOARD", "Specifies the keyboard protocol for Qt for Embedded Linux.", "Auto", "Auto, linuxinput, qvfbkbd", "https://doc.qt.io/archives/qt-4.8/qws.html", "Embedded Linux"),
            new EnvVar("QT_QWS_SHM", "Enables shared memory support in Qt for Embedded Linux.", "1", "0, 1", "https://doc.qt.io/archives/qt-4.8/qws.html", "Embedded Linux"),
            new EnvVar("QT_QWS_DEPTH", "Specifies the color depth for Qt for Embedded Linux.", "16", "16, 24, 32", "https://doc.qt.io/archives/qt-4.8/qws.html", "Embedded Linux"),
            new EnvVar("QT_QWS_NO_TRANSFORMATIONS", "Disables transformations in Qt for Embedded Linux.", "0", "0, 1", "https://doc.qt.io/archives/qt-4.8/qws.html", "Embedded Linux"),
            new EnvVar("QT_QWS_SERVER", "Specifies the Qt Windowing System (QWS) server.", "None", "Any valid server path", "https://doc.qt.io/archives/qt-4.8/qws.html", "Embedded Linux"),
            new EnvVar("QT_REDUCE_DEBUG_OUTPUT", "Reduces the amount of debug output generated.", "0", "0, 1", "https://doc.qt.io/qt-5/qdebug.html", "Debugging"),
            new EnvVar("QT_SCROLL_BAR_FADE_DURATION", "Sets the fade duration for scroll bars.", "400", "Any positive integer", "https://doc.qt.io/qt-5/qscrollbar.html", "Widgets"),
            new EnvVar("QT_SCROLL_BAR_POLICY", "Specifies the scroll bar policy.", "Auto", "Auto, AlwaysOn, AlwaysOff", "https://doc.qt.io/qt-5/qscrollarea.html", "Widgets"),
            new EnvVar("QT_STYLE_SHEET_LOADING", "Enables style sheet loading from an external file.", "0", "0, 1", "https://doc.qt.io/qt-5/stylesheet.html", "Styling"),
            new EnvVar("QT_TOUCHPAD_SCROLLING", "Enables touchpad scrolling in the application.", "1", "0, 1", "https://doc.qt.io/qt-5/qscrollarea.html", "Input"),
            new EnvVar("QT_USE_NATIVE_DIALOGS", "Forces the use of native file dialogs.", "1", "0, 1", "https://doc.qt.io/qt-5/qfiledialog.html", "General"),
            new EnvVar("QT_USE_SOFTWARE_OPENGL", "Forces the use of software rendering for OpenGL.", "0", "0, 1", "https://doc.qt.io/qt-5/qopenglwidget.html", "Graphics"),
            new EnvVar("QT_WAYLAND_DISABLE_INPUT_METHOD", "Disables the input method on Wayland.", "0", "0, 1", "https://doc.qt.io/qt-5/wayland.html", "Wayland"),
            new EnvVar("QT_WAYLAND_FORCE_DPI", "Forces the DPI setting on Wayland.", "None", "Any positive integer", "https://doc.qt.io/qt-5/wayland.html", "Wayland"),
            new EnvVar("QT_WAYLAND_USE_NATIVE_WINDOWS", "Forces the use of native windows on Wayland.", "0", "0, 1", "https://doc.qt.io/qt-5/wayland.html", "Wayland"),
            new EnvVar("QT_WIDGET_ANIMATION_SPEED", "Sets the speed of widget animations.", "1.0", "Any positive floating-point value", "https://doc.qt.io/qt-5/qwidget.html", "Styling")
        }.OrderBy(env => env.Name).ToList();
    }
}


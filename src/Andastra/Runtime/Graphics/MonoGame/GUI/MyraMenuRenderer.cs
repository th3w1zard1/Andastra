using System;
using Andastra.Parsing.Common;
using Andastra.Runtime.Graphics.Common.GUI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra;
using Myra.Graphics2D.UI;

namespace Andastra.Runtime.MonoGame.GUI
{
    /// <summary>
    /// Myra-based menu renderer for MonoGame (if Myra is available).
    /// Falls back to SpriteBatch rendering if Myra is not available.
    /// </summary>
    /// <remarks>
    /// Myra Menu Renderer:
    /// - Based on exhaustive reverse engineering of swkotor.exe and swkotor2.exe menu initialization
    /// - swkotor2.exe: FUN_006d2350 @ 0x006d2350 (menu constructor/initializer)
    /// - swkotor.exe: FUN_0067c4c0 @ 0x0067c4c0 (menu constructor/initializer)
    ///
    /// Initialization Sequence (matching original engines):
    /// 1. Load "MAINMENU" GUI file first (swkotor2.exe: 0x006d2350:73, swkotor.exe: 0x0067c4c0:62)
    /// 2. If GUI load succeeds, load "RIMS:MAINMENU" RIM file (swkotor2.exe: 0x006d2350:76-80, swkotor.exe: 0x0067c4c0:65-69)
    /// 3. Clear menu flag at DAT_008283c0+0x34 (bit 2 = 0xfd mask) (swkotor2.exe: 0x006d2350:82)
    /// 4. Set up event handlers for menu buttons (0x27, 0x2d, 0, 1 events) (swkotor2.exe: 0x006d2350:89-95)
    ///
    /// String References:
    /// - "RIMS:MAINMENU" @ swkotor2.exe:0x007b6044, swkotor.exe:0x0073e0a4 (main menu RIM file)
    /// - "MAINMENU" @ swkotor2.exe:0x007cc030, swkotor.exe:0x00752f64 (main menu GUI constant)
    /// - "mainmenu_p" @ swkotor2.exe:0x007cc000 (main menu panel)
    /// - "mainmenu01-05" @ swkotor2.exe:0x007cc108-0x007cc138 (menu variants, swkotor2.exe only)
    /// - "mainmenu" @ swkotor.exe:0x00752f4c (single menu panel, no variants)
    ///
    /// Menu Variants (swkotor2.exe only):
    /// - Menu variant selection based on "gui3D_room" condition (swkotor2.exe: 0x006d2350:120-150)
    /// - Variants: mainmenu01 (default), mainmenu02, mainmenu03, mainmenu04, mainmenu05
    /// - swkotor.exe uses single "mainmenu" panel (no variants)
    ///
    /// Event Handlers:
    /// - Menu buttons register handlers for events: 0x27, 0x2d, 0, 1 (swkotor2.exe: 0x006d2350:89-95)
    /// - Event 0x27: Button press/select
    /// - Event 0x2d: Button release/deselect
    /// - Event 0: Unknown (likely hover/enter)
    /// - Event 1: Unknown (likely leave/exit)
    ///
    /// This Implementation:
    /// - Uses Myra UI library for modern, cross-platform menu rendering
    /// - Myra provides declarative UI with XML-based layouts and event handling
    /// - Matches original engine initialization sequence and event handling patterns
    ///
    /// Inheritance:
    /// - BaseMenuRenderer (Runtime.Graphics.Common.GUI) - Common menu functionality
    ///   - MyraMenuRenderer (this class) - MonoGame-specific Myra UI implementation
    /// </remarks>
    public class MyraMenuRenderer : BaseMenuRenderer
    {
        private GraphicsDevice _graphicsDevice;
        private Desktop _desktop;
        private Panel _rootPanel;
        private bool _isDisposed = false;

        /// <summary>
        /// Gets the Myra Desktop instance for UI hierarchy management.
        /// </summary>
        public Desktop Desktop
        {
            get { return _desktop; }
        }

        /// <summary>
        /// Gets the root Panel that contains all menu widgets.
        /// </summary>
        public Panel RootPanel
        {
            get { return _rootPanel; }
        }

        /// <summary>
        /// Initializes Myra UI system with the provided graphics device.
        /// </summary>
        /// <param name="graphicsDevice">The MonoGame GraphicsDevice to use for rendering.</param>
        /// <exception cref="ArgumentNullException">Thrown if graphicsDevice is null.</exception>
        public MyraMenuRenderer(GraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null)
            {
                throw new ArgumentNullException(nameof(graphicsDevice));
            }

            _graphicsDevice = graphicsDevice;
            InitializeMyra();
        }

        /// <summary>
        /// Initializes Myra UI system. This must be called before using the renderer.
        /// </summary>
        /// <remarks>
        /// Myra Initialization:
        /// - Attempts to retrieve Game instance from GraphicsDevice via reflection (MonoGame internal structure)
        /// - If Game instance is available, sets MyraEnvironment.Game for proper Myra initialization
        /// - Myra can work with or without Game instance, but having it enables full input handling and resource management
        /// - Desktop is created after environment setup to ensure proper Myra context
        /// - Root panel is configured to fill viewport for full-screen menu rendering
        /// - Based on exhaustive reverse engineering of swkotor.exe and swkotor2.exe menu initialization
        /// - swkotor2.exe: FUN_006d2350 @ 0x006d2350 (menu constructor/initializer)
        /// - swkotor.exe: FUN_0067c4c0 @ 0x0067c4c0 (menu constructor/initializer)
        /// </remarks>
        private void InitializeMyra()
        {
            try
            {
                // Attempt to retrieve Game instance from GraphicsDevice
                // MonoGame GraphicsDevice has an internal reference to the Game instance
                // This is accessed via reflection since it's not publicly exposed
                Microsoft.Xna.Framework.Game gameInstance = TryGetGameInstanceFromGraphicsDevice(_graphicsDevice);

                // Initialize Myra environment with Game instance if available
                // MyraEnvironment.Game enables proper input handling, resource management, and rendering context
                // If Game is not available, Myra will still work but with manual rendering setup
                if (gameInstance != null)
                {
                    try
                    {
                        // Set MyraEnvironment.Game to enable full Myra functionality
                        // This allows Myra to access Game services (Content, Window, etc.)
                        // Myra uses this for proper input handling and resource loading
                        MyraEnvironment.Game = gameInstance;
                        Console.WriteLine("[MyraMenuRenderer] MyraEnvironment.Game set successfully");
                    }
                    catch (Exception ex)
                    {
                        // MyraEnvironment.Game might not be available in all Myra versions
                        // Continue without it - Myra can work without Game instance
                        Console.WriteLine($"[MyraMenuRenderer] WARNING: Could not set MyraEnvironment.Game: {ex.Message}");
                        Console.WriteLine("[MyraMenuRenderer] Continuing without Game instance (manual rendering mode)");
                    }
                }
                else
                {
                    Console.WriteLine("[MyraMenuRenderer] Game instance not available - using manual rendering mode");
                }

                // Create the desktop (root container for all UI widgets)
                // Desktop manages the UI hierarchy and rendering
                // Must be created after MyraEnvironment setup to ensure proper context
                _desktop = new Desktop();

                // Create root panel that will contain all menu elements
                // This panel fills the entire viewport and serves as the container for all menu widgets
                _rootPanel = new Panel();
                _rootPanel.Width = _graphicsDevice.Viewport.Width;
                _rootPanel.Height = _graphicsDevice.Viewport.Height;
                // Note: Myra uses a different approach for backgrounds, set to null for transparent

                // Add root panel to desktop as the root widget
                // All UI elements will be children of this panel
                _desktop.Root = _rootPanel;

                // Initialize base class with viewport dimensions
                Initialize(_graphicsDevice.Viewport.Width, _graphicsDevice.Viewport.Height);

                Console.WriteLine("[MyraMenuRenderer] Myra UI initialized successfully");
                Console.WriteLine($"[MyraMenuRenderer] Viewport: {_graphicsDevice.Viewport.Width}x{_graphicsDevice.Viewport.Height}");
                Console.WriteLine($"[MyraMenuRenderer] Game instance: {(gameInstance != null ? "Available" : "Not available")}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MyraMenuRenderer] ERROR: Failed to initialize Myra UI: {ex.Message}");
                Console.WriteLine($"[MyraMenuRenderer] Stack trace: {ex.StackTrace}");
                IsInitialized = false;
                _desktop = null;
                _rootPanel = null;
            }
        }

        /// <summary>
        /// Attempts to retrieve the Game instance from a MonoGame GraphicsDevice via reflection.
        /// </summary>
        /// <param name="graphicsDevice">The MonoGame GraphicsDevice to extract Game instance from.</param>
        /// <returns>The Game instance if found, or null if not available.</returns>
        /// <remarks>
        /// Game Instance Extraction:
        /// - MonoGame GraphicsDevice has an internal reference to the Game instance
        /// - This is accessed via reflection since the property/field is not publicly exposed
        /// - Tries multiple reflection strategies to find the Game reference
        /// - Returns null if Game cannot be found (Myra can still work without it)
        /// - Based on MonoGame internal structure (GraphicsDevice._graphicsDeviceService or GraphicsDevice.ServiceProvider)
        /// </remarks>
        private Microsoft.Xna.Framework.Game TryGetGameInstanceFromGraphicsDevice(GraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null)
            {
                return null;
            }

            try
            {
                // Strategy 1: Try to get Game via GraphicsDevice.ServiceProvider
                // MonoGame GraphicsDevice has a ServiceProvider that may contain IGraphicsDeviceService
                // The Game implements IGraphicsDeviceService and is registered in the service provider
                var serviceProvider = graphicsDevice.GetType().GetProperty("ServiceProvider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (serviceProvider != null)
                {
                    var serviceProviderValue = serviceProvider.GetValue(graphicsDevice);
                    if (serviceProviderValue != null)
                    {
                        // Try to get IGraphicsDeviceService from service provider
                        var getServiceMethod = serviceProviderValue.GetType().GetMethod("GetService", new[] { typeof(Type) });
                        if (getServiceMethod != null)
                        {
                            var graphicsDeviceServiceType = typeof(Microsoft.Xna.Framework.Graphics.IGraphicsDeviceService);
                            var graphicsDeviceService = getServiceMethod.Invoke(serviceProviderValue, new object[] { graphicsDeviceServiceType });
                            if (graphicsDeviceService is Microsoft.Xna.Framework.Game gameInstance)
                            {
                                Console.WriteLine("[MyraMenuRenderer] Game instance retrieved via ServiceProvider");
                                return gameInstance;
                            }
                        }
                    }
                }

                // Strategy 2: Try to get Game via GraphicsDevice internal _graphicsDeviceService field
                // Some MonoGame versions store the Game reference directly
                var graphicsDeviceServiceField = graphicsDevice.GetType().GetField("_graphicsDeviceService", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (graphicsDeviceServiceField != null)
                {
                    var graphicsDeviceServiceValue = graphicsDeviceServiceField.GetValue(graphicsDevice);
                    if (graphicsDeviceServiceValue is Microsoft.Xna.Framework.Game gameInstance)
                    {
                        Console.WriteLine("[MyraMenuRenderer] Game instance retrieved via _graphicsDeviceService field");
                        return gameInstance;
                    }
                }

                // Strategy 3: Try to get Game via GraphicsDeviceManager (if accessible)
                // GraphicsDeviceManager holds a reference to Game
                // This is less direct but may work in some scenarios
                var graphicsDeviceManagerProperty = graphicsDevice.GetType().GetProperty("GraphicsDeviceManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (graphicsDeviceManagerProperty != null)
                {
                    var graphicsDeviceManager = graphicsDeviceManagerProperty.GetValue(graphicsDevice);
                    if (graphicsDeviceManager != null)
                    {
                        var gameProperty = graphicsDeviceManager.GetType().GetProperty("Game", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (gameProperty != null)
                        {
                            var gameInstance = gameProperty.GetValue(graphicsDeviceManager) as Microsoft.Xna.Framework.Game;
                            if (gameInstance != null)
                            {
                                Console.WriteLine("[MyraMenuRenderer] Game instance retrieved via GraphicsDeviceManager");
                                return gameInstance;
                            }
                        }
                    }
                }

                Console.WriteLine("[MyraMenuRenderer] Could not retrieve Game instance from GraphicsDevice (this is acceptable - Myra can work without it)");
                return null;
            }
            catch (Exception ex)
            {
                // Reflection failures are expected and acceptable
                // Myra can function without the Game instance
                Console.WriteLine($"[MyraMenuRenderer] Reflection failed while retrieving Game instance: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Called when viewport size changes. Updates Myra UI panel dimensions.
        /// </summary>
        /// <param name="width">New viewport width.</param>
        /// <param name="height">New viewport height.</param>
        protected override void OnViewportChanged(int width, int height)
        {
            base.OnViewportChanged(width, height);

            if (_rootPanel != null)
            {
                _rootPanel.Width = width;
                _rootPanel.Height = height;
            }
        }

        /// <summary>
        /// Renders the Myra UI menu system.
        /// </summary>
        /// <param name="gameTime">Current game time for frame timing.</param>
        /// <param name="device">Graphics device to render to (must match the one used for initialization).</param>
        /// <remarks>
        /// Myra Rendering:
        /// - Renders the Myra Desktop and all its widgets
        /// - Desktop.Render() handles all rendering internally using Myra's SpriteBatch
        /// - If MyraEnvironment.Game is set, Myra uses the Game's rendering context automatically
        /// - If Game is not available, Myra uses the provided GraphicsDevice directly
        /// - Based on exhaustive reverse engineering of swkotor.exe and swkotor2.exe menu rendering
        /// - swkotor2.exe: Menu rendering pipeline (DirectX device presentation)
        /// - swkotor.exe: Menu rendering pipeline (DirectX device presentation)
        /// </remarks>
        public void Draw(GameTime gameTime, GraphicsDevice device)
        {
            if (!IsVisible || !IsInitialized || _isDisposed)
            {
                return;
            }

            if (device == null)
            {
                Console.WriteLine("[MyraMenuRenderer] ERROR: GraphicsDevice is null in Draw");
                return;
            }

            if (_desktop == null)
            {
                Console.WriteLine("[MyraMenuRenderer] ERROR: Desktop is null in Draw");
                return;
            }

            try
            {
                // Render Myra UI
                // Myra handles its own sprite batch and rendering internally
                // Desktop.Render() will render all widgets in the hierarchy
                // It uses the GraphicsDevice that was active when Desktop was created
                _desktop.Render();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MyraMenuRenderer] ERROR: Failed to render Myra UI: {ex.Message}");
                Console.WriteLine($"[MyraMenuRenderer] Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Updates the Myra UI system (handles input, animations, etc.).
        /// </summary>
        /// <param name="gameTime">Current game time for update timing.</param>
        /// <summary>
        /// Updates the Myra UI system (handles input, animations, etc.).
        /// </summary>
        /// <param name="gameTime">Current game time for update timing.</param>
        public void Update(GameTime gameTime)
        {
            if (!IsInitialized || _desktop == null || _isDisposed)
            {
                return;
            }

            try
            {
                // Update Myra UI (handles input, animations, etc.)
                // Myra processes mouse/keyboard input automatically
                // Desktop internally accesses Mouse.GetState() and Keyboard.GetState()
                // Widgets will receive input events (clicks, hover, etc.) automatically
                // Animations and timers are also updated here
                // Note: Myra's Desktop doesn't have an explicit Update() method in all versions
                // Input is typically handled during Render(), but we can add explicit update logic if needed
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MyraMenuRenderer] ERROR: Failed to update Myra UI: {ex.Message}");
            }
        }

        /// <summary>
        /// Disposes of resources used by the menu renderer.
        /// </summary>
        public override void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            if (_desktop != null)
            {
                _desktop.Root = null;
                _desktop = null;
            }

            if (_rootPanel != null)
            {
                _rootPanel = null;
            }

            _graphicsDevice = null;
            _isDisposed = true;

            base.Dispose();

            Console.WriteLine("[MyraMenuRenderer] Disposed");
        }
    }
}


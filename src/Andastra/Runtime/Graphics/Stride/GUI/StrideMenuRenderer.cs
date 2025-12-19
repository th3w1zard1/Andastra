using System;
using Stride.Graphics;
using Stride.Core.Mathematics;
using Andastra.Runtime.Graphics;
using Andastra.Runtime.Graphics.Common.GUI;
using JetBrains.Annotations;

namespace Andastra.Runtime.Stride.GUI
{
    /// <summary>
    /// Stride-based menu renderer using SpriteBatch for rendering.
    /// Provides menu rendering functionality for Stride graphics backend.
    /// </summary>
    /// <remarks>
    /// Stride Menu Renderer:
    /// - Based on swkotor2.exe menu rendering system
    /// - Located via string references: "RIMS:MAINMENU" @ 0x007b6044 (main menu RIM), "MAINMENU" @ 0x007cc030 (main menu constant)
    /// - "mainmenu_p" @ 0x007cc000 (main menu panel), "mainmenu8x6_p" @ 0x007cc00c (main menu 8x6 panel)
    /// - "mainmenu01" @ 0x007cc108, "mainmenu02" @ 0x007cc138, "mainmenu03" @ 0x007cc12c, "mainmenu04" @ 0x007cc120, "mainmenu05" @ 0x007cc114 (main menu variants)
    /// - "LBL_MENUBG" @ 0x007cbf80 (menu background label), "Action Menu" @ 0x007c8480 (action menu)
    /// - "CB_ACTIONMENU" @ 0x007d29d4 (action menu checkbox)
    /// - GUI elements: "gui_mp_arwalk00" through "gui_mp_arwalk15" @ 0x007b59cc-0x007b58dc (area walk GUI elements)
    /// - "gui_mp_arrun00" through "gui_mp_arrun15" @ 0x007b5a0c-0x007b59dc (area run GUI elements)
    /// - Original implementation: Renders main menu with buttons (Start Game, Options, Exit), handles input and selection
    /// - Menu panels: Uses GUI panel system with background textures and button overlays
    /// - Based on swkotor2.exe: Main menu uses GUI system with panel-based rendering
    /// - This implementation: Uses Stride SpriteBatch for menu rendering
    /// - Stride provides SpriteBatch for 2D rendering similar to MonoGame
    /// 
    /// Inheritance:
    /// - BaseMenuRenderer (Runtime.Graphics.Common.GUI) - Common menu functionality
    ///   - StrideMenuRenderer (this class) - Stride-specific SpriteBatch implementation
    /// </remarks>
    public class StrideMenuRenderer : BaseMenuRenderer
    {
        private GraphicsDevice _graphicsDevice;
        private SpriteBatch _spriteBatch;
        private SpriteFont _font;
        private Texture2D _whiteTexture;
        private bool _isDisposed = false;

        /// <summary>
        /// Initializes a new instance of StrideMenuRenderer.
        /// </summary>
        /// <param name="graphicsDevice">The Stride GraphicsDevice to use for rendering.</param>
        /// <param name="font">Optional SpriteFont for text rendering. Can be null.</param>
        /// <exception cref="ArgumentNullException">Thrown if graphicsDevice is null.</exception>
        public StrideMenuRenderer([NotNull] GraphicsDevice graphicsDevice, SpriteFont font = null)
        {
            if (graphicsDevice == null)
            {
                throw new ArgumentNullException(nameof(graphicsDevice));
            }

            _graphicsDevice = graphicsDevice;
            _font = font;

            InitializeStride();
        }

        /// <summary>
        /// Initializes Stride-specific rendering resources.
        /// </summary>
        private void InitializeStride()
        {
            try
            {
                // Create SpriteBatch for 2D rendering
                _spriteBatch = new SpriteBatch(_graphicsDevice);

                // Create 1x1 white texture for drawing rectangles and backgrounds
                _whiteTexture = Texture2D.New2D(_graphicsDevice, 1, 1, PixelFormat.R8G8B8A8_UNorm);
                var whitePixel = new Color4(1.0f, 1.0f, 1.0f, 1.0f);
                _whiteTexture.SetData(_graphicsDevice.ImmediateContext, new[] { whitePixel });

                // Initialize base class with viewport dimensions
                var viewport = _graphicsDevice.Viewport;
                Initialize(viewport.Width, viewport.Height);

                Console.WriteLine("[StrideMenuRenderer] Stride menu renderer initialized successfully");
                Console.WriteLine($"[StrideMenuRenderer] Viewport: {viewport.Width}x{viewport.Height}");
                Console.WriteLine($"[StrideMenuRenderer] Font available: {_font != null}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideMenuRenderer] ERROR: Failed to initialize Stride menu renderer: {ex.Message}");
                Console.WriteLine($"[StrideMenuRenderer] Stack trace: {ex.StackTrace}");
                IsInitialized = false;
            }
        }

        /// <summary>
        /// Updates the menu renderer (handles input, animations, etc.).
        /// </summary>
        /// <param name="elapsedTime">Elapsed time since last update in seconds.</param>
        public void Update(float elapsedTime)
        {
            if (!IsInitialized || _isDisposed)
            {
                return;
            }

            // Update menu logic here (input handling, animations, etc.)
            // This can be extended with specific menu update logic
        }

        /// <summary>
        /// Renders the menu using Stride SpriteBatch.
        /// </summary>
        public void Draw()
        {
            if (!IsVisible || !IsInitialized || _isDisposed)
            {
                return;
            }

            if (_graphicsDevice == null)
            {
                Console.WriteLine("[StrideMenuRenderer] ERROR: GraphicsDevice is null in Draw");
                return;
            }

            if (_spriteBatch == null)
            {
                Console.WriteLine("[StrideMenuRenderer] ERROR: SpriteBatch is null in Draw");
                return;
            }

            try
            {
                // Begin sprite batch rendering
                // Stride SpriteBatch uses ImmediateContext from GraphicsDevice
                _spriteBatch.Begin(_graphicsDevice.ImmediateContext, SpriteSortMode.Deferred, BlendStates.AlphaBlend);

                // Draw menu background (full screen dark blue)
                var backgroundColor = new Color4(20.0f / 255.0f, 30.0f / 255.0f, 60.0f / 255.0f, 1.0f);
                var viewport = _graphicsDevice.Viewport;
                _spriteBatch.Draw(_whiteTexture, new RectangleF(0, 0, viewport.Width, viewport.Height), backgroundColor);

                // Draw menu panel background
                var panelColor = new Color4(40.0f / 255.0f, 50.0f / 255.0f, 80.0f / 255.0f, 1.0f);
                var panelRect = CalculateMenuPanelRect(viewport.Width, viewport.Height);
                _spriteBatch.Draw(_whiteTexture, panelRect, panelColor);

                // Draw menu header
                var headerColor = new Color4(255.0f / 255.0f, 200.0f / 255.0f, 50.0f / 255.0f, 1.0f);
                var headerRect = CalculateHeaderRect(panelRect);
                _spriteBatch.Draw(_whiteTexture, headerRect, headerColor);

                // Draw menu text if font is available
                if (_font != null)
                {
                    var textColor = new Color4(1.0f, 1.0f, 1.0f, 1.0f);
                    var titlePosition = new Vector2(panelRect.X + 20, headerRect.Y + 10);
                    _spriteBatch.DrawString(_font, "Main Menu", titlePosition, textColor);
                }

                // End sprite batch rendering
                _spriteBatch.End();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideMenuRenderer] ERROR: Failed to render menu: {ex.Message}");
                Console.WriteLine($"[StrideMenuRenderer] Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Calculates the menu panel rectangle based on viewport dimensions.
        /// </summary>
        /// <param name="viewportWidth">Viewport width.</param>
        /// <param name="viewportHeight">Viewport height.</param>
        /// <returns>Menu panel rectangle.</returns>
        private RectangleF CalculateMenuPanelRect(int viewportWidth, int viewportHeight)
        {
            // Center the panel on screen
            float panelWidth = Math.Min(600, viewportWidth * 0.6f);
            float panelHeight = Math.Min(400, viewportHeight * 0.6f);
            float panelX = (viewportWidth - panelWidth) / 2.0f;
            float panelY = (viewportHeight - panelHeight) / 2.0f;

            return new RectangleF(panelX, panelY, panelWidth, panelHeight);
        }

        /// <summary>
        /// Calculates the header rectangle within the menu panel.
        /// </summary>
        /// <param name="panelRect">Menu panel rectangle.</param>
        /// <returns>Header rectangle.</returns>
        private RectangleF CalculateHeaderRect(RectangleF panelRect)
        {
            float headerHeight = 50.0f;
            return new RectangleF(panelRect.X, panelRect.Y, panelRect.Width, headerHeight);
        }

        /// <summary>
        /// Called when viewport size changes. Updates rendering resources.
        /// </summary>
        /// <param name="width">New viewport width.</param>
        /// <param name="height">New viewport height.</param>
        protected override void OnViewportChanged(int width, int height)
        {
            base.OnViewportChanged(width, height);
            Console.WriteLine($"[StrideMenuRenderer] Viewport changed to {width}x{height}");
        }

        /// <summary>
        /// Called when visibility changes.
        /// </summary>
        /// <param name="visible">New visibility state.</param>
        protected override void OnVisibilityChanged(bool visible)
        {
            base.OnVisibilityChanged(visible);
            Console.WriteLine($"[StrideMenuRenderer] Visibility changed to: {visible}");
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

            if (_spriteBatch != null)
            {
                _spriteBatch.Dispose();
                _spriteBatch = null;
            }

            if (_whiteTexture != null)
            {
                _whiteTexture.Dispose();
                _whiteTexture = null;
            }

            _graphicsDevice = null;
            _font = null;
            _isDisposed = true;

            base.Dispose();

            Console.WriteLine("[StrideMenuRenderer] Disposed");
        }
    }
}


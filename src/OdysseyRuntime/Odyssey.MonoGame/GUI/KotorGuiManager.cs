using System;
using System.IO;
using Stride.Engine;
using Stride.Graphics;
using CSharpKOTOR.Resource.Generics.GUI;
using CSharpKOTOR.Resources;
using CSharpKOTOR.Installation;
using CSharpKOTOR.Formats.GFF;
using CSharpKOTOR.Formats.TPC;
using SearchLocation = CSharpKOTOR.Installation.SearchLocation;

namespace Odyssey.MonoGame.GUI
{
    /// <summary>
    /// Manages loading and rendering of KOTOR GUI files.
    /// Handles resource loading via CSharpKOTOR.Installation, GUI state, and event handling.
    /// </summary>
    public class KotorGuiManager
    {
        private readonly UIComponent _uiComponent;
        private readonly GraphicsDevice _graphicsDevice;
        private readonly Installation _installation;
        private readonly KotorGuiRenderer _renderer;

        private CSharpKOTOR.Resource.Generics.GUI.GUI _currentGui;

        /// <summary>
        /// Event fired when a GUI button is clicked.
        /// </summary>
        public event EventHandler<GuiButtonClickedEventArgs> OnButtonClicked;

        // Initialize KotorGuiManager with UI component and graphics device
        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.UIComponent.html
        // UIComponent manages UI rendering and input handling
        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Graphics.GraphicsDevice.html
        // GraphicsDevice provides access to graphics hardware and resources
        // Source: https://doc.stride3d.net/latest/en/manual/graphics/low-level-api/index.html
        public KotorGuiManager(UIComponent uiComponent, GraphicsDevice graphicsDevice, string gamePath)
        {
            _uiComponent = uiComponent ?? throw new ArgumentNullException(nameof(uiComponent));
            _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));

            // Initialize Installation for resource loading
            try
            {
                _installation = new Installation(gamePath);
                Console.WriteLine($"[KotorGuiManager] Installation initialized: {_installation.Game}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[KotorGuiManager] ERROR: Failed to initialize Installation: {ex.Message}");
                throw;
            }

            _renderer = new KotorGuiRenderer(uiComponent, _graphicsDevice, this);
        }

        /// <summary>
        /// Loads and displays a GUI from game resources.
        /// Automatically creates a fallback UI if loading fails.
        /// </summary>
        /// <param name="guiResRef">Resource reference (e.g. "mainmenu8x6_p" for main menu)</param>
        /// <param name="screenWidth">Target screen width</param>
        /// <param name="screenHeight">Target screen height</param>
        /// <param name="useFallbackOnFailure">If true, creates a fallback UI when loading fails</param>
        public bool LoadGui(string guiResRef, int screenWidth, int screenHeight, bool useFallbackOnFailure = true)
        {
            if (string.IsNullOrEmpty(guiResRef))
            {
                Console.WriteLine("[KotorGuiManager] ERROR: GUI resource reference is null or empty");
                if (useFallbackOnFailure)
                {
                    Console.WriteLine("[KotorGuiManager] KOTOR GUI loading failed, using fallback");
                    CreateFallbackUI(screenWidth, screenHeight);
                }
                return false;
            }

            try
            {
                Console.WriteLine($"[KotorGuiManager] Loading GUI: {guiResRef}");

                // Load GUI file from game resources
                byte[] guiData = LoadGuiResource(guiResRef);
                if (guiData == null || guiData.Length == 0)
                {
                    Console.WriteLine($"[KotorGuiManager] ERROR: Failed to load GUI resource: {guiResRef}");
                    if (useFallbackOnFailure)
                    {
                        Console.WriteLine("[KotorGuiManager] KOTOR GUI loading failed, using fallback");
                        CreateFallbackUI(screenWidth, screenHeight);
                    }
                    return false;
                }

                // Parse GUI using GUIReader
                var reader = new GUIReader(guiData);
                _currentGui = reader.Load();

                if (_currentGui == null)
                {
                    Console.WriteLine($"[KotorGuiManager] ERROR: Failed to parse GUI: {guiResRef}");
                    if (useFallbackOnFailure)
                    {
                        Console.WriteLine("[KotorGuiManager] KOTOR GUI loading failed, using fallback");
                        CreateFallbackUI(screenWidth, screenHeight);
                    }
                    return false;
                }

                Console.WriteLine($"[KotorGuiManager] Loaded GUI '{_currentGui.Tag}' with {_currentGui.Controls.Count} controls");

                // Render GUI
                _renderer.RenderGui(_currentGui, screenWidth, screenHeight);

                Console.WriteLine($"[KotorGuiManager] Successfully rendered GUI: {guiResRef}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[KotorGuiManager] ERROR loading GUI '{guiResRef}': {ex.Message}");
                Console.WriteLine($"[KotorGuiManager] Stack trace: {ex.StackTrace}");
                if (useFallbackOnFailure)
                {
                    Console.WriteLine("[KotorGuiManager] KOTOR GUI loading failed, using fallback");
                    CreateFallbackUI(screenWidth, screenHeight);
                }
                return false;
            }
        }

        /// <summary>
        /// Loads a GUI resource from game files using Installation.
        /// </summary>
        private byte[] LoadGuiResource(string resRef)
        {
            try
            {
                // Search in override first, then base game
                var searchOrder = new SearchLocation[] {
                    SearchLocation.OVERRIDE,
                    //SearchLocation.MODULES,  // TODO: determine whether we need to load from here too??
                    SearchLocation.CHITIN
                };

                var result = _installation.Resource(resRef, ResourceType.GUI, searchOrder, null);
                if (result != null && result.Data != null && result.Data.Length > 0)
                {
                    Console.WriteLine($"[KotorGuiManager] Found GUI resource: {resRef} ({result.Data.Length} bytes)");
                    return result.Data;
                }

                Console.WriteLine($"[KotorGuiManager] GUI resource not found: {resRef}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[KotorGuiManager] ERROR loading GUI resource '{resRef}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Loads a texture resource from game files.
        /// </summary>
        // Load texture from game resources and return Stride Texture
        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Graphics.Texture.html
        // Texture represents image data for rendering, returned type is Texture class
        // Method signature: Texture LoadTexture(string resRef)
        // Returns null if texture cannot be loaded or converted
        // Source: https://doc.stride3d.net/latest/en/manual/graphics/low-level-api/textures-and-render-textures.html
        public Texture LoadTexture(string resRef)
        {
            if (string.IsNullOrEmpty(resRef))
            {
                return null;
            }

            try
            {
                var searchOrder = new SearchLocation[] {
                    SearchLocation.OVERRIDE,
                    SearchLocation.TEXTURES_GUI,
                    SearchLocation.TEXTURES_TPA,
                    SearchLocation.TEXTURES_TPB,
                    SearchLocation.TEXTURES_TPC,
                    SearchLocation.CHITIN
                };

                var result = _installation.Resource(resRef, ResourceType.TPC, searchOrder, null);
                if (result == null || result.Data == null)
                {
                    // Try TGA format
                    result = _installation.Resource(resRef, ResourceType.TGA, searchOrder, null);
                }

                if (result != null && result.Data != null)
                {
                    // Load TPC/TGA and convert to Stride Texture
                    // For now, return null - texture loading needs to be implemented
                    Console.WriteLine($"[KotorGuiManager] Found texture: {resRef} ({result.Data.Length} bytes)");
                    // TODO: Convert TPC/TGA to Stride Texture
                    return null;
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[KotorGuiManager] ERROR loading texture '{resRef}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Triggers a button click event.
        /// </summary>
        internal void TriggerButtonClick(string buttonTag, int? buttonId)
        {
            OnButtonClicked?.Invoke(this, new GuiButtonClickedEventArgs(buttonTag, buttonId));
        }

        /// <summary>
        /// Creates a fallback UI when KOTOR GUI loading fails.
        /// Uses system defaults and doesn't rely on external resources or fonts.
        /// </summary>
        public void CreateFallbackUI(int screenWidth, int screenHeight)
        {
            Console.WriteLine("[KotorGuiManager] Creating fallback UI");
            _renderer.CreateFallbackUI(screenWidth, screenHeight);
        }

        /// <summary>
        /// Clears the currently displayed GUI.
        /// </summary>
        // Clear the currently rendered GUI from the UI component
        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.UIComponent.html
        // UIComponent.Page property can be set to null to clear the active UI page
        // Method signature: UIPage Page { get; set; }
        // Source: https://doc.stride3d.net/latest/en/manual/user-interface/index.html
        public void Clear()
        {
            _renderer.Clear();
            _currentGui = null;
        }
    }

    /// <summary>
    /// Event args for GUI button clicks.
    /// </summary>
    public class GuiButtonClickedEventArgs : EventArgs
    {
        public string ButtonTag { get; }
        public int? ButtonId { get; }

        public GuiButtonClickedEventArgs(string buttonTag, int? buttonId)
        {
            ButtonTag = buttonTag;
            ButtonId = buttonId;
        }
    }
}

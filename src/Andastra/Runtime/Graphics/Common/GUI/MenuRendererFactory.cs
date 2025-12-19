using System;
using Andastra.Runtime.Graphics;
using Andastra.Runtime.Graphics.Common.Enums;
using JetBrains.Annotations;
using MonoGame = Andastra.Runtime.MonoGame;
using Stride = Andastra.Runtime.Stride;

namespace Andastra.Runtime.Graphics.Common.GUI
{
    /// <summary>
    /// Factory for creating menu renderers based on graphics backend type.
    /// </summary>
    /// <remarks>
    /// Menu Renderer Factory:
    /// - Creates appropriate menu renderer based on graphics backend (MonoGame, Stride)
    /// - Menu renderers are backend-specific but engine-agnostic (work for all engines)
    /// - Based on exhaustive reverse engineering of original engine menu initialization
    /// - All engines (Odyssey, Aurora, Eclipse, Infinity) use the same menu renderer interface
    /// - Engine-specific menu initialization is handled by the game session, not the renderer
    /// </remarks>
    public static class MenuRendererFactory
    {
        /// <summary>
        /// Creates a menu renderer for the specified graphics backend.
        /// </summary>
        /// <param name="graphicsBackend">The graphics backend to create a menu renderer for.</param>
        /// <returns>A menu renderer instance, or null if the backend type is not supported.</returns>
        /// <exception cref="ArgumentNullException">Thrown if graphicsBackend is null.</exception>
        public static BaseMenuRenderer CreateMenuRenderer([NotNull] IGraphicsBackend graphicsBackend)
        {
            if (graphicsBackend == null)
            {
                throw new ArgumentNullException(nameof(graphicsBackend));
            }

            if (!graphicsBackend.IsInitialized)
            {
                throw new InvalidOperationException("Graphics backend must be initialized before creating menu renderer");
            }

            var backendType = graphicsBackend.BackendType;
            var graphicsDevice = graphicsBackend.GraphicsDevice;

            switch (backendType)
            {
                case GraphicsBackendType.MonoGame:
                    // MonoGame uses Myra UI library for menu rendering
                    // MyraMenuRenderer requires MonoGame GraphicsDevice
                    var monoGameDevice = GetMonoGameGraphicsDevice(graphicsDevice);
                    if (monoGameDevice != null)
                    {
                        return new MonoGame.GUI.MyraMenuRenderer(monoGameDevice);
                    }
                    break;

                case GraphicsBackendType.Stride:
                    // Stride uses SpriteBatch for menu rendering
                    // StrideMenuRenderer requires Stride GraphicsDevice
                    var strideDevice = GetStrideGraphicsDevice(graphicsDevice);
                    if (strideDevice != null)
                    {
                        // Try to load font (optional)
                        Stride.Graphics.SpriteFont font = null;
                        try
                        {
                            font = graphicsBackend.ContentManager?.Load<Stride.Graphics.SpriteFont>("Fonts/Arial");
                        }
                        catch
                        {
                            // Font loading failed, continue without font
                        }
                        return new Stride.GUI.StrideMenuRenderer(strideDevice, font);
                    }
                    break;

                default:
                    Console.WriteLine($"[MenuRendererFactory] Unsupported graphics backend type: {backendType}");
                    return null;
            }

            Console.WriteLine($"[MenuRendererFactory] Failed to create menu renderer for backend: {backendType}");
            return null;
        }

        /// <summary>
        /// Gets the MonoGame GraphicsDevice from the abstraction layer.
        /// </summary>
        private static Microsoft.Xna.Framework.Graphics.GraphicsDevice GetMonoGameGraphicsDevice(IGraphicsDevice device)
        {
            // The MonoGame implementation wraps the native GraphicsDevice
            // MonoGameGraphicsDevice has a private _device field that contains the actual GraphicsDevice
            if (device is MonoGame.Graphics.MonoGameGraphicsDevice monoGameDevice)
            {
                // Use reflection to access the private _device field
                var deviceType = monoGameDevice.GetType();
                var deviceField = deviceType.GetField("_device", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (deviceField != null)
                {
                    try
                    {
                        return deviceField.GetValue(monoGameDevice) as Microsoft.Xna.Framework.Graphics.GraphicsDevice;
                    }
                    catch
                    {
                        // Reflection failed
                    }
                }
            }

            // Try direct cast if device is already the right type (shouldn't happen, but handle gracefully)
            if (device is Microsoft.Xna.Framework.Graphics.GraphicsDevice mgDevice)
            {
                return mgDevice;
            }

            return null;
        }

        /// <summary>
        /// Gets the Stride GraphicsDevice from the abstraction layer.
        /// </summary>
        private static Stride.Graphics.GraphicsDevice GetStrideGraphicsDevice(IGraphicsDevice device)
        {
            // The Stride implementation wraps the native GraphicsDevice
            // StrideGraphicsDevice has a private _device field that contains the actual GraphicsDevice
            if (device is Stride.Graphics.StrideGraphicsDevice strideDevice)
            {
                // Use reflection to access the private _device field
                var deviceType = strideDevice.GetType();
                var deviceField = deviceType.GetField("_device", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (deviceField != null)
                {
                    try
                    {
                        return deviceField.GetValue(strideDevice) as Stride.Graphics.GraphicsDevice;
                    }
                    catch
                    {
                        // Reflection failed
                    }
                }
            }

            // Try direct cast if device is already the right type (shouldn't happen, but handle gracefully)
            if (device is Stride.Graphics.GraphicsDevice strideGraphicsDevice)
            {
                return strideGraphicsDevice;
            }

            return null;
        }
    }
}


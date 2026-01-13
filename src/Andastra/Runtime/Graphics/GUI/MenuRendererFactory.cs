using System;
using System.Reflection;
using Andastra.Runtime.Graphics;
using Andastra.Runtime.Graphics.Common.Enums;
using JetBrains.Annotations;

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
                    return CreateMonoGameMenuRenderer(graphicsDevice);

                case GraphicsBackendType.Stride:
                    return CreateStrideMenuRenderer(graphicsDevice);

                default:
                    Console.WriteLine($"[MenuRendererFactory] Unsupported graphics backend type: {backendType}");
                    return null;
            }

            Console.WriteLine($"[MenuRendererFactory] Failed to create menu renderer for backend: {backendType}");
            return null;
        }

        /// <summary>
        /// Creates a MonoGame menu renderer using Myra UI library via reflection.
        /// </summary>
        /// <param name="graphicsDevice">The graphics device wrapper from the backend.</param>
        /// <returns>A MyraMenuRenderer instance, or null if creation fails.</returns>
        /// <remarks>
        /// MonoGame Menu Renderer Creation:
        /// - Uses reflection to dynamically load MonoGame types to avoid circular dependencies
        /// - Extracts the underlying MonoGame GraphicsDevice from the IGraphicsDevice wrapper
        /// - Creates MyraMenuRenderer with the extracted GraphicsDevice
        /// - Handles errors gracefully with detailed logging
        /// - Based on exhaustive reverse engineering of swkotor.exe and swkotor2.exe menu initialization
        /// - All engines (Odyssey, Aurora, Eclipse, Infinity) use the same menu renderer interface
        /// </remarks>
        private static BaseMenuRenderer CreateMonoGameMenuRenderer(IGraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null)
            {
                Console.WriteLine("[MenuRendererFactory] ERROR: GraphicsDevice is null for MonoGame backend");
                return null;
            }

            try
            {
                // Get the type of the graphics device wrapper
                Type graphicsDeviceType = graphicsDevice.GetType();

                // Check if it's a MonoGameGraphicsDevice (using string comparison to avoid compile-time dependency)
                if (graphicsDeviceType.FullName != "Andastra.Game.Graphics.MonoGame.Graphics.MonoGameGraphicsDevice")
                {
                    Console.WriteLine($"[MenuRendererFactory] ERROR: GraphicsDevice is not a MonoGameGraphicsDevice (type: {graphicsDeviceType.FullName})");
                    return null;
                }

                // Extract the underlying MonoGame GraphicsDevice using reflection
                object mgGraphicsDevice = ExtractMonoGameGraphicsDevice(graphicsDevice);

                if (mgGraphicsDevice == null)
                {
                    Console.WriteLine("[MenuRendererFactory] ERROR: Failed to extract MonoGame GraphicsDevice from wrapper");
                    return null;
                }

                // Load the MyraMenuRenderer type using reflection
                Assembly monoGameAssembly = graphicsDeviceType.Assembly;
                Type myraMenuRendererType = monoGameAssembly.GetType("Andastra.Game.Graphics.MonoGame.GUI.MyraMenuRenderer");

                if (myraMenuRendererType == null)
                {
                    Console.WriteLine("[MenuRendererFactory] ERROR: Could not find MyraMenuRenderer type in MonoGame assembly");
                    return null;
                }

                // Create MyraMenuRenderer instance with the extracted GraphicsDevice
                object renderer = Activator.CreateInstance(myraMenuRendererType, new[] { mgGraphicsDevice });

                if (renderer == null)
                {
                    Console.WriteLine("[MenuRendererFactory] ERROR: Failed to create MyraMenuRenderer instance");
                    return null;
                }

                // Cast to BaseMenuRenderer (this should work since MyraMenuRenderer inherits from it)
                if (!(renderer is BaseMenuRenderer baseRenderer))
                {
                    Console.WriteLine($"[MenuRendererFactory] ERROR: Created instance is not a BaseMenuRenderer (type: {renderer.GetType().FullName})");
                    return null;
                }

                // Verify initialization using reflection
                PropertyInfo isInitializedProperty = baseRenderer.GetType().GetProperty("IsInitialized");
                if (isInitializedProperty != null)
                {
                    object isInitialized = isInitializedProperty.GetValue(baseRenderer);
                    if (isInitialized is bool initialized && !initialized)
                    {
                        Console.WriteLine("[MenuRendererFactory] WARNING: MyraMenuRenderer was created but not initialized");
                    }
                }

                // Get viewport dimensions using reflection for logging
                PropertyInfo viewportWidthProperty = baseRenderer.GetType().GetProperty("ViewportWidth");
                PropertyInfo viewportHeightProperty = baseRenderer.GetType().GetProperty("ViewportHeight");
                if (viewportWidthProperty != null && viewportHeightProperty != null)
                {
                    object width = viewportWidthProperty.GetValue(baseRenderer);
                    object height = viewportHeightProperty.GetValue(baseRenderer);
                    Console.WriteLine($"[MenuRendererFactory] Successfully created MonoGame menu renderer (MyraMenuRenderer)");
                    Console.WriteLine($"[MenuRendererFactory] Viewport: {width}x{height}");
                }
                else
                {
                    Console.WriteLine($"[MenuRendererFactory] Successfully created MonoGame menu renderer (MyraMenuRenderer)");
                }

                return baseRenderer;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MenuRendererFactory] ERROR: Exception while creating MonoGame menu renderer: {ex.Message}");
                Console.WriteLine($"[MenuRendererFactory] Exception type: {ex.GetType().Name}");
                Console.WriteLine($"[MenuRendererFactory] Stack trace: {ex.StackTrace}");

                // Re-throw if it's a critical exception that should propagate
                if (ex is OutOfMemoryException || ex is StackOverflowException)
                {
                    throw;
                }

                return null;
            }
        }

        /// <summary>
        /// Creates a Stride menu renderer using SpriteBatch via reflection.
        /// </summary>
        /// <param name="graphicsDevice">The graphics device wrapper from the backend.</param>
        /// <returns>A StrideMenuRenderer instance, or null if creation fails.</returns>
        /// <remarks>
        /// Stride Menu Renderer Creation:
        /// - Uses reflection to dynamically load Stride types to avoid circular dependencies
        /// - Extracts the underlying Stride GraphicsDevice from the IGraphicsDevice wrapper
        /// - Creates StrideMenuRenderer with the extracted GraphicsDevice
        /// - Handles errors gracefully with detailed logging
        /// - Based on exhaustive reverse engineering of swkotor.exe and swkotor2.exe menu initialization
        /// - swkotor2.exe: 0x006d2350 @ 0x006d2350 (menu constructor/initializer)
        /// - swkotor.exe: 0x0067c4c0 @ 0x0067c4c0 (menu constructor/initializer)
        /// - All engines (Odyssey, Aurora, Eclipse, Infinity) use the same menu renderer interface
        /// </remarks>
        private static BaseMenuRenderer CreateStrideMenuRenderer(IGraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null)
            {
                Console.WriteLine("[MenuRendererFactory] ERROR: GraphicsDevice is null for Stride backend");
                return null;
            }

            try
            {
                // Get the type of the graphics device wrapper
                Type graphicsDeviceType = graphicsDevice.GetType();

                // Check if it's a StrideGraphicsDevice (using string comparison to avoid compile-time dependency)
                if (graphicsDeviceType.FullName != "Runtime.Stride.Graphics.StrideGraphicsDevice")
                {
                    Console.WriteLine($"[MenuRendererFactory] ERROR: GraphicsDevice is not a StrideGraphicsDevice (type: {graphicsDeviceType.FullName})");
                    return null;
                }

                // Extract the underlying Stride GraphicsDevice using reflection
                object strideGraphicsDevice = ExtractStrideGraphicsDevice(graphicsDevice);

                if (strideGraphicsDevice == null)
                {
                    Console.WriteLine("[MenuRendererFactory] ERROR: Failed to extract Stride GraphicsDevice from wrapper");
                    return null;
                }

                // Load the StrideMenuRenderer type using reflection
                Assembly strideAssembly = graphicsDeviceType.Assembly;
                Type strideMenuRendererType = strideAssembly.GetType("Runtime.Stride.GUI.StrideMenuRenderer");

                if (strideMenuRendererType == null)
                {
                    Console.WriteLine("[MenuRendererFactory] ERROR: Could not find StrideMenuRenderer type in Stride assembly");
                    return null;
                }

                // Create StrideMenuRenderer instance with the extracted GraphicsDevice
                // StrideMenuRenderer constructor takes (GraphicsDevice graphicsDevice, SpriteFont font = null)
                // We'll pass null for font since it's optional
                object renderer = Activator.CreateInstance(strideMenuRendererType, new[] { strideGraphicsDevice, null });

                if (renderer == null)
                {
                    Console.WriteLine("[MenuRendererFactory] ERROR: Failed to create StrideMenuRenderer instance");
                    return null;
                }

                // Cast to BaseMenuRenderer (this should work since StrideMenuRenderer inherits from it)
                if (!(renderer is BaseMenuRenderer baseRenderer))
                {
                    Console.WriteLine($"[MenuRendererFactory] ERROR: Created instance is not a BaseMenuRenderer (type: {renderer.GetType().FullName})");
                    return null;
                }

                // Verify initialization using reflection
                PropertyInfo isInitializedProperty = baseRenderer.GetType().GetProperty("IsInitialized");
                if (isInitializedProperty != null)
                {
                    object isInitialized = isInitializedProperty.GetValue(baseRenderer);
                    if (isInitialized is bool initialized && !initialized)
                    {
                        Console.WriteLine("[MenuRendererFactory] WARNING: StrideMenuRenderer was created but not initialized");
                    }
                }

                // Get viewport dimensions using reflection for logging
                PropertyInfo viewportWidthProperty = baseRenderer.GetType().GetProperty("ViewportWidth");
                PropertyInfo viewportHeightProperty = baseRenderer.GetType().GetProperty("ViewportHeight");
                if (viewportWidthProperty != null && viewportHeightProperty != null)
                {
                    object width = viewportWidthProperty.GetValue(baseRenderer);
                    object height = viewportHeightProperty.GetValue(baseRenderer);
                    Console.WriteLine($"[MenuRendererFactory] Successfully created Stride menu renderer (StrideMenuRenderer)");
                    Console.WriteLine($"[MenuRendererFactory] Viewport: {width}x{height}");
                }
                else
                {
                    Console.WriteLine($"[MenuRendererFactory] Successfully created Stride menu renderer (StrideMenuRenderer)");
                }

                return baseRenderer;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MenuRendererFactory] ERROR: Exception while creating Stride menu renderer: {ex.Message}");
                Console.WriteLine($"[MenuRendererFactory] Exception type: {ex.GetType().Name}");
                Console.WriteLine($"[MenuRendererFactory] Stack trace: {ex.StackTrace}");

                // Re-throw if it's a critical exception that should propagate
                if (ex is OutOfMemoryException || ex is StackOverflowException)
                {
                    throw;
                }

                return null;
            }
        }

        /// <summary>
        /// Extracts the underlying MonoGame GraphicsDevice from the MonoGameGraphicsDevice wrapper using reflection.
        /// </summary>
        /// <param name="wrapper">The MonoGameGraphicsDevice wrapper instance.</param>
        /// <returns>The underlying MonoGame GraphicsDevice, or null if extraction fails.</returns>
        /// <remarks>
        /// GraphicsDevice Extraction:
        /// - Uses reflection to access the private _device field in MonoGameGraphicsDevice
        /// - This is necessary because the wrapper doesn't expose the underlying device publicly
        /// - Handles reflection errors gracefully with detailed logging
        /// - Returns null if the field cannot be accessed or is null
        /// </remarks>
        private static object ExtractMonoGameGraphicsDevice(object wrapper)
        {
            if (wrapper == null)
            {
                Console.WriteLine("[MenuRendererFactory] ERROR: MonoGameGraphicsDevice wrapper is null");
                return null;
            }

            try
            {
                // Get the type of MonoGameGraphicsDevice
                Type wrapperType = wrapper.GetType();

                // Get the private _device field using reflection
                // BindingFlags.NonPublic | BindingFlags.Instance to access private instance field
                FieldInfo deviceField = wrapperType.GetField("_device", BindingFlags.NonPublic | BindingFlags.Instance);

                if (deviceField == null)
                {
                    Console.WriteLine("[MenuRendererFactory] ERROR: Could not find _device field in MonoGameGraphicsDevice");
                    FieldInfo[] allFields = wrapperType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                    string fieldNames = string.Join(", ", Array.ConvertAll(allFields, f => f.Name));
                    Console.WriteLine($"[MenuRendererFactory] Available fields: {fieldNames}");
                    return null;
                }

                // Get the value of the _device field from the wrapper instance
                object deviceValue = deviceField.GetValue(wrapper);

                if (deviceValue == null)
                {
                    Console.WriteLine("[MenuRendererFactory] ERROR: _device field is null in MonoGameGraphicsDevice wrapper");
                    return null;
                }

                Console.WriteLine($"[MenuRendererFactory] Successfully extracted MonoGame GraphicsDevice (type: {deviceValue.GetType().FullName})");
                return deviceValue;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MenuRendererFactory] ERROR: Exception while extracting GraphicsDevice: {ex.Message}");
                Console.WriteLine($"[MenuRendererFactory] Exception type: {ex.GetType().Name}");
                Console.WriteLine($"[MenuRendererFactory] Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Extracts the underlying Stride GraphicsDevice from the StrideGraphicsDevice wrapper using reflection.
        /// </summary>
        /// <param name="wrapper">The StrideGraphicsDevice wrapper instance.</param>
        /// <returns>The underlying Stride GraphicsDevice, or null if extraction fails.</returns>
        /// <remarks>
        /// GraphicsDevice Extraction:
        /// - Uses reflection to access the private _device field in StrideGraphicsDevice
        /// - This is necessary because the wrapper doesn't expose the underlying device publicly
        /// - Handles reflection errors gracefully with detailed logging
        /// - Returns null if the field cannot be accessed or is null
        /// - Based on exhaustive reverse engineering of swkotor.exe and swkotor2.exe menu initialization
        /// - swkotor2.exe: 0x006d2350 @ 0x006d2350 (menu constructor/initializer)
        /// - swkotor.exe: 0x0067c4c0 @ 0x0067c4c0 (menu constructor/initializer)
        /// </remarks>
        private static object ExtractStrideGraphicsDevice(object wrapper)
        {
            if (wrapper == null)
            {
                Console.WriteLine("[MenuRendererFactory] ERROR: StrideGraphicsDevice wrapper is null");
                return null;
            }

            try
            {
                // Get the type of StrideGraphicsDevice
                Type wrapperType = wrapper.GetType();

                // Get the private _device field using reflection
                // BindingFlags.NonPublic | BindingFlags.Instance to access private instance field
                FieldInfo deviceField = wrapperType.GetField("_device", BindingFlags.NonPublic | BindingFlags.Instance);

                if (deviceField == null)
                {
                    Console.WriteLine("[MenuRendererFactory] ERROR: Could not find _device field in StrideGraphicsDevice");
                    FieldInfo[] allFields = wrapperType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                    string fieldNames = string.Join(", ", Array.ConvertAll(allFields, f => f.Name));
                    Console.WriteLine($"[MenuRendererFactory] Available fields: {fieldNames}");
                    return null;
                }

                // Get the value of the _device field from the wrapper instance
                object deviceValue = deviceField.GetValue(wrapper);

                if (deviceValue == null)
                {
                    Console.WriteLine("[MenuRendererFactory] ERROR: _device field is null in StrideGraphicsDevice wrapper");
                    return null;
                }

                Console.WriteLine($"[MenuRendererFactory] Successfully extracted Stride GraphicsDevice (type: {deviceValue.GetType().FullName})");
                return deviceValue;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MenuRendererFactory] ERROR: Exception while extracting Stride GraphicsDevice: {ex.Message}");
                Console.WriteLine($"[MenuRendererFactory] Exception type: {ex.GetType().Name}");
                Console.WriteLine($"[MenuRendererFactory] Stack trace: {ex.StackTrace}");
                return null;
            }
        }

    }
}


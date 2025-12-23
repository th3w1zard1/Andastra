using System;
using System.Reflection;
using Stride.Engine;
using Stride.Graphics;
using Stride.Core.Mathematics;
using Andastra.Runtime.Graphics;
using Andastra.Runtime.Graphics.Common.Enums;

namespace Andastra.Runtime.Stride.Graphics
{
    /// <summary>
    /// Stride implementation of IGraphicsBackend.
    /// </summary>
    public class StrideGraphicsBackend : IGraphicsBackend
    {
        private StrideGameWrapper _game;
        private StrideGraphicsDevice _graphicsDevice;
        private StrideContentManager _contentManager;
        private StrideWindow _window;
        private StrideInputManager _inputManager;
        private bool _isInitialized;
        private bool? _desiredVSyncState;

        public GraphicsBackendType BackendType => GraphicsBackendType.Stride;

        public IGraphicsDevice GraphicsDevice => _graphicsDevice;

        public IContentManager ContentManager => _contentManager;

        public IWindow Window => _window;

        public IInputManager InputManager => _inputManager;

        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Gets whether the graphics backend supports VSync (vertical synchronization).
        /// Stride supports VSync through GraphicsDevice.Presenter when initialized.
        /// </summary>
        public bool SupportsVSync => _isInitialized && _graphicsDevice != null && _game?.GraphicsDevice?.Presenter != null;

        public StrideGraphicsBackend()
        {
            _game = new StrideGameWrapper();
        }

        public void Initialize(int width, int height, string title, bool fullscreen = false)
        {
            if (_isInitialized)
            {
                return;
            }

            // Stride initialization happens in the game constructor
            // We'll set up the window properties before running
            _game.Window.ClientSize = new Int2(width, height);
            _game.Window.Title = title;
            _game.Window.IsFullscreen = fullscreen;
            _game.Window.IsMouseVisible = true;

            // Initialize graphics device, content manager, window, and input after game starts
            // These will be set up in the Run method when the game is actually running
            _isInitialized = true;
        }

        public void Run(Action<float> updateAction, Action drawAction)
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("Backend must be initialized before running.");
            }

            // Initialize graphics components when game starts
            if (_graphicsDevice == null)
            {
                // In Stride, CommandList is obtained from GraphicsDevice.ResourceFactory or created per-frame
                // For immediate rendering, we'll create a CommandList from the GraphicsDevice
                // TODO: STUB - CommandList creation might need to be per-frame in Stride 4.x
                // For now, we'll pass null and handle it in StrideGraphicsDevice
                StrideGraphics.CommandList commandList = null;
                try
                {
                    // Try to get CommandList from GraphicsDevice.ResourceFactory if available
                    // In Stride 4.x, CommandList is typically created per-frame from ResourceFactory
                    // For immediate context, we'll need to create it when needed
                    if (_game.GraphicsDevice != null)
                    {
                        // CommandList will be created on-demand in StrideGraphicsDevice when needed
                        // For now, pass null - the device will handle CommandList creation
                    }
                }
                catch
                {
                    // CommandList creation failed, will be handled in StrideGraphicsDevice
                }

                _graphicsDevice = new StrideGraphicsDevice(_game.GraphicsDevice, commandList);
                _contentManager = new StrideContentManager(_game.Content, commandList);
                _window = new StrideWindow(_game.Window);
                _inputManager = new StrideInputManager(_game.Input);

                // Register the CommandList for use by ImmediateContext() extension method
                // In Stride, we'll need to create and register CommandList per-frame
                // For now, we'll register it when it becomes available
                // TODO: STUB - CommandList should be created per-frame and registered each frame
                if (_game.GraphicsDevice != null && commandList != null)
                {
                    GraphicsDeviceExtensions.RegisterCommandList(_game.GraphicsDevice, commandList);
                }

                // Apply VSync state if it was set before graphics device was initialized
                // Based on swkotor2.exe: VSync controlled via DirectX Present parameters (PresentationInterval)
                // Stride equivalent: GraphicsDevice.Presenter.VSyncMode
                if (_desiredVSyncState.HasValue)
                {
                    ApplyVSyncState(_desiredVSyncState.Value);
                }
            }

            // Stride uses a different game loop pattern
            // We'll need to hook into the game's update and draw callbacks
            _game.UpdateFrame += (sender, e) =>
            {
                BeginFrame();
                if (updateAction != null)
                {
                    updateAction((float)e.Elapsed.TotalSeconds);
                }
            };

            _game.DrawFrame += (sender, e) =>
            {
                if (drawAction != null)
                {
                    drawAction();
                }
                EndFrame();
            };

            _game.Run();
        }

        public void Exit()
        {
            _game.Exit();
        }

        public void BeginFrame()
        {
            _inputManager.Update();
        }

        public void EndFrame()
        {
            // Stride handles presentation automatically
        }

        public IRoomMeshRenderer CreateRoomMeshRenderer()
        {
            if (!_isInitialized || _graphicsDevice == null)
            {
                throw new InvalidOperationException("Backend must be initialized before creating renderers.");
            }
            return new StrideRoomMeshRenderer(_game.GraphicsDevice);
        }

        public IEntityModelRenderer CreateEntityModelRenderer(object gameDataManager = null, object installation = null)
        {
            if (!_isInitialized || _graphicsDevice == null)
            {
                throw new InvalidOperationException("Backend must be initialized before creating renderers.");
            }
            return new StrideEntityModelRenderer(_game.GraphicsDevice, gameDataManager, installation);
        }

        public ISpatialAudio CreateSpatialAudio()
        {
            return new StrideSpatialAudio();
        }

        public object CreateDialogueCameraController(object cameraController)
        {
            if (cameraController is Andastra.Runtime.Core.Camera.CameraController coreCameraController)
            {
                return new Odyssey.Stride.Camera.StrideDialogueCameraController(coreCameraController);
            }
            throw new ArgumentException("Camera controller must be a CameraController instance", nameof(cameraController));
        }

        public object CreateSoundPlayer(object resourceProvider)
        {
            if (resourceProvider is Andastra.Runtime.Content.Interfaces.IGameResourceProvider provider)
            {
                var spatialAudio = CreateSpatialAudio();
                return new Andastra.Runtime.Stride.Audio.StrideSoundPlayer(provider, spatialAudio);
            }
            throw new ArgumentException("Resource provider must be an IGameResourceProvider instance", nameof(resourceProvider));
        }

        public object CreateMusicPlayer(object resourceProvider)
        {
            if (resourceProvider is Andastra.Runtime.Content.Interfaces.IGameResourceProvider provider)
            {
                return new Andastra.Runtime.Stride.Audio.StrideMusicPlayer(provider);
            }
            throw new ArgumentException("Resource provider must be an IGameResourceProvider instance", nameof(resourceProvider));
        }

        public object CreateVoicePlayer(object resourceProvider)
        {
            if (resourceProvider is Andastra.Runtime.Content.Interfaces.IGameResourceProvider provider)
            {
                var spatialAudio = CreateSpatialAudio();
                return new Andastra.Runtime.Stride.Audio.StrideVoicePlayer(provider, spatialAudio);
            }
            throw new ArgumentException("Resource provider must be an IGameResourceProvider instance", nameof(resourceProvider));
        }

        /// <summary>
        /// Sets the VSync (vertical synchronization) state.
        /// </summary>
        /// <param name="enabled">True to enable VSync, false to disable it.</param>
        /// <remarks>
        /// VSync Implementation:
        /// - Based on Stride GraphicsDevice.Presenter.VSyncMode
        /// - Original game: VSync controlled via DirectX Present parameters (swkotor2.exe: DirectX device presentation)
        ///   - VSync enabled: D3DPRESENT_INTERVAL_ONE (0x00000001) - synchronize with vertical refresh
        ///   - VSync disabled: D3DPRESENT_INTERVAL_IMMEDIATE (0x80000000) - present immediately
        /// - Stride uses GraphicsDevice.Presenter to control VSync
        /// - Changes are applied immediately to the swap chain
        /// - VSync synchronizes frame rendering with monitor refresh rate to prevent screen tearing
        /// - Stride VSyncMode: None (disabled), VerticalSync (enabled), or Adaptive (adaptive sync)
        /// </remarks>
        public void SetVSync(bool enabled)
        {
            // Store desired VSync state for later application if graphics device is not yet initialized
            _desiredVSyncState = enabled;

            if (!_isInitialized || _graphicsDevice == null || _game?.GraphicsDevice == null)
            {
                // Graphics device not yet initialized - state will be applied when it becomes available
                Console.WriteLine($"[Stride] VSync {(enabled ? "enabled" : "disabled")} requested (will be applied when graphics device is initialized)");
                return;
            }

            // Apply VSync state immediately if graphics device is available
            ApplyVSyncState(enabled);
        }

        /// <summary>
        /// Attempts to parse an enum value with multiple fallback names.
        /// C# 7.3 compatible - uses Enum.Parse instead of Enum.TryParse.
        /// </summary>
        /// <param name="enumType">The enum type to parse.</param>
        /// <param name="names">Array of enum value names to try in order.</param>
        /// <returns>The parsed enum value, or null if all names failed.</returns>
        private object TryParseEnumValue(Type enumType, params string[] names)
        {
            foreach (var name in names)
            {
                try
                {
                    return Enum.Parse(enumType, name, true);
                }
                catch (ArgumentException)
                {
                    // Try next name
                    continue;
                }
            }
            return null;
        }

        /// <summary>
        /// Applies VSync state to the Stride GraphicsDevice.Presenter.
        /// Based on swkotor2.exe: DirectX Present parameters control VSync via PresentationInterval
        /// </summary>
        /// <param name="enabled">True to enable VSync, false to disable it.</param>
        private void ApplyVSyncState(bool enabled)
        {
            try
            {
                var presenter = _game.GraphicsDevice?.Presenter;
                if (presenter == null)
                {
                    Console.WriteLine("[Stride] WARNING: Cannot apply VSync - Presenter not available");
                    return;
                }

                // Apply VSync via GraphicsDevice.Presenter
                // Based on Stride 4.2 API: GraphicsPresenter controls VSync through PresentMode or VSyncMode property
                // Original game equivalent (swkotor2.exe DirectX Present parameters):
                //   - VSync enabled: D3DPRESENT_INTERVAL_ONE (0x00000001) - synchronize with vertical refresh
                //   - VSync disabled: D3DPRESENT_INTERVAL_IMMEDIATE (0x80000000) - present immediately

                // Try to find and set VSyncMode property first (preferred in Stride 4.2)
                var vsyncModeProperty = typeof(GraphicsPresenter).GetProperty("VSyncMode", BindingFlags.Public | BindingFlags.Instance);
                if (vsyncModeProperty != null && vsyncModeProperty.CanWrite)
                {
                    var enumType = vsyncModeProperty.PropertyType;
                    object enumValue = null;

                    // Try to parse enum values - Stride uses PresentMode enum with values: None, VerticalSync, Adaptive
                    // Original game equivalent:
                    //   - VSync enabled: D3DPRESENT_INTERVAL_ONE (0x00000001)
                    //   - VSync disabled: D3DPRESENT_INTERVAL_IMMEDIATE (0x80000000)
                    if (enabled)
                    {
                        enumValue = TryParseEnumValue(enumType, "VerticalSync", "VSync", "Sync");
                    }
                    else
                    {
                        enumValue = TryParseEnumValue(enumType, "None", "Immediate", "Unlimited");
                    }

                    if (enumValue != null)
                    {
                        vsyncModeProperty.SetValue(presenter, enumValue);
                        Console.WriteLine($"[Stride] VSync {(enabled ? "enabled" : "disabled")} via VSyncMode property");
                        return;
                    }
                }

                // Fallback: Try PresentMode property (some Stride versions use this)
                var presentModeProperty = typeof(GraphicsPresenter).GetProperty("PresentMode", BindingFlags.Public | BindingFlags.Instance);
                if (presentModeProperty != null && presentModeProperty.CanWrite)
                {
                    var enumType = presentModeProperty.PropertyType;
                    object enumValue = null;

                    // Try to parse enum values with fallback names
                    if (enabled)
                    {
                        enumValue = TryParseEnumValue(enumType, "VerticalSync", "VSync", "Sync");
                    }
                    else
                    {
                        enumValue = TryParseEnumValue(enumType, "Immediate", "None", "Unlimited");
                    }

                    if (enumValue != null)
                    {
                        presentModeProperty.SetValue(presenter, enumValue);
                        Console.WriteLine($"[Stride] VSync {(enabled ? "enabled" : "disabled")} via PresentMode property");
                        return;
                    }
                }

                // If we reach here, neither property was found or enum parsing failed
                Console.WriteLine("[Stride] WARNING: Cannot find VSyncMode or PresentMode property on GraphicsPresenter, or enum values are not recognized");
                Console.WriteLine($"[Stride] VSync state requested: {(enabled ? "enabled" : "disabled")} (not applied)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Stride] ERROR: Failed to apply VSync state: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"[Stride] Inner exception: {ex.InnerException.Message}");
                }
            }
        }

        public void Dispose()
        {
            if (_graphicsDevice != null)
            {
                _graphicsDevice.Dispose();
                _graphicsDevice = null;
            }

            if (_contentManager != null)
            {
                _contentManager.Dispose();
                _contentManager = null;
            }

            // Unregister CommandList when disposing
            if (_game != null && _game.GraphicsDevice != null)
            {
                GraphicsDeviceExtensions.UnregisterCommandList(_game.GraphicsDevice);
                _game.Dispose();
                _game = null;
            }
        }
    }
}


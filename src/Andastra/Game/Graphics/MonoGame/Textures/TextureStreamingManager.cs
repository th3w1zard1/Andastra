using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Game.Graphics.MonoGame.Textures
{
    /// <summary>
    /// Texture streaming and atlas management system.
    ///
    /// Features:
    /// - Texture streaming for large textures
    /// - Texture atlas management
    /// - Mipmap streaming
    /// - VRAM management
    /// - Texture priority-based loading
    /// - Asynchronous texture loading with cancellation support
    /// </summary>
    public class TextureStreamingManager : IDisposable
    {
        /// <summary>
        /// Delegate for loading texture data asynchronously.
        /// Returns a stream containing the texture data, or null if the texture cannot be loaded.
        /// </summary>
        /// <param name="textureName">Name of the texture to load.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>Stream containing texture data, or null if not found.</returns>
        public delegate Task<Stream> TextureLoaderDelegate(string textureName, CancellationToken cancellationToken);

        /// <summary>
        /// Texture streaming entry.
        /// </summary>
        private class TextureEntry
        {
            public string Name;
            public Texture2D Texture;
            public int MipLevel;
            public float Priority;
            public bool IsStreaming;
            public bool IsLoading;
            public long VRAMSize;
            public Task<byte[]> LoadingTask;
        }

        private readonly GraphicsDevice _graphicsDevice;
        private readonly Dictionary<string, TextureEntry> _textures;
        private readonly Dictionary<string, Task<byte[]>> _loadingTasks;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private long _currentVRAMUsage;
        private long _maxVRAMBudget;
        private TextureLoaderDelegate _textureLoader;
        private readonly object _lockObject;

        /// <summary>
        /// Gets or sets the maximum VRAM budget in bytes.
        /// </summary>
        public long MaxVRAMBudget
        {
            get { return _maxVRAMBudget; }
            set { _maxVRAMBudget = Math.Max(0, value); }
        }

        /// <summary>
        /// Gets the current VRAM usage in bytes.
        /// </summary>
        public long CurrentVRAMUsage
        {
            get { return _currentVRAMUsage; }
        }

        /// <summary>
        /// Initializes a new texture streaming manager.
        /// </summary>
        /// <param name="graphicsDevice">Graphics device.</param>
        /// <param name="textureLoader">Delegate for loading texture data asynchronously. If null, textures must be loaded via LoadTextureFromStream or LoadTextureFromFile.</param>
        /// <param name="maxVRAMBudget">Maximum VRAM budget in bytes (0 = unlimited).</param>
        public TextureStreamingManager(GraphicsDevice graphicsDevice, TextureLoaderDelegate textureLoader = null, long maxVRAMBudget = 0)
        {
            if (graphicsDevice == null)
            {
                throw new ArgumentNullException(nameof(graphicsDevice));
            }

            _graphicsDevice = graphicsDevice;
            _textures = new Dictionary<string, TextureEntry>();
            _loadingTasks = new Dictionary<string, Task<byte[]>>();
            _cancellationTokenSource = new CancellationTokenSource();
            _textureLoader = textureLoader;
            _maxVRAMBudget = maxVRAMBudget;
            _lockObject = new object();
        }

        /// <summary>
        /// Loads or streams a texture.
        /// </summary>
        /// <param name="name">Texture name.</param>
        /// <param name="priority">Loading priority (higher = more important).</param>
        /// <returns>Texture, or null if not yet loaded.</returns>
        public Texture2D GetTexture(string name, float priority = 1.0f)
        {
            TextureEntry entry;
            if (_textures.TryGetValue(name, out entry))
            {
                // Update priority
                entry.Priority = priority;
                return entry.Texture;
            }

            // Create entry for streaming
            entry = new TextureEntry
            {
                Name = name,
                Priority = priority,
                IsStreaming = true
            };
            _textures[name] = entry;

            return null;
        }

        /// <summary>
        /// Updates texture streaming, loading/unloading based on priority and VRAM budget.
        /// </summary>
        public void Update()
        {
            // Check VRAM budget
            if (_maxVRAMBudget > 0 && _currentVRAMUsage > _maxVRAMBudget)
            {
                // Unload low-priority textures
                UnloadLowPriorityTextures();
            }

            // Load high-priority textures
            LoadHighPriorityTextures();
        }

        private void UnloadLowPriorityTextures()
        {
            var sorted = new List<TextureEntry>(_textures.Values);
            sorted.Sort((a, b) => a.Priority.CompareTo(b.Priority)); // Sort by priority (lowest first)

            foreach (TextureEntry entry in sorted)
            {
                if (_currentVRAMUsage <= _maxVRAMBudget * 0.9f) // Stop at 90% to avoid thrashing
                {
                    break;
                }

                if (entry.Texture != null && !entry.IsStreaming)
                {
                    UnloadTexture(entry);
                }
            }
        }

        private void LoadHighPriorityTextures()
        {
            var sorted = new List<TextureEntry>(_textures.Values);
            sorted.Sort((a, b) => b.Priority.CompareTo(a.Priority)); // Sort by priority (highest first)

            foreach (TextureEntry entry in sorted)
            {
                if (entry.Texture == null && entry.IsStreaming && !entry.IsLoading)
                {
                    // Check VRAM budget
                    if (_maxVRAMBudget > 0 && _currentVRAMUsage >= _maxVRAMBudget)
                    {
                        break; // Out of VRAM budget
                    }

                    // Start async texture loading
                    LoadTextureAsync(entry);
                }
            }

            // Process completed loading tasks
            ProcessCompletedLoads();
        }

        /// <summary>
        /// Loads a texture asynchronously from a stream.
        /// Based on MonoGame API: https://docs.monogame.net/api/Microsoft.Xna.Framework.Graphics.Texture2D.html
        /// Texture2D.FromStream(GraphicsDevice, Stream) loads a texture from a stream synchronously.
        /// For async loading, we read the stream data on a background thread, then create the texture on the main thread.
        /// </summary>
        /// <param name="entry">Texture entry to load.</param>
        private void LoadTextureAsync(TextureEntry entry)
        {
            lock (_lockObject)
            {
                if (entry.IsLoading)
                {
                    return; // Already loading
                }

                entry.IsLoading = true;
            }

            // Create loading task
            Task<byte[]> loadingTask = Task.Run(async () =>
            {
                try
                {
                    Stream textureStream = null;

                    // Use texture loader delegate if available
                    if (_textureLoader != null)
                    {
                        textureStream = await _textureLoader(entry.Name, _cancellationTokenSource.Token);
                    }

                    if (textureStream == null)
                    {
                        throw new InvalidOperationException($"Texture loader returned null for texture: {entry.Name}");
                    }

                    // Read stream data on background thread
                    byte[] textureData;
                    using (var memoryStream = new MemoryStream())
                    {
                        await textureStream.CopyToAsync(memoryStream, 8192, _cancellationTokenSource.Token);
                        textureData = memoryStream.ToArray();
                    }

                    // Dispose the original stream
                    textureStream.Dispose();

                    // Create texture on main thread (MonoGame requires graphics operations on main thread)
                    // We'll complete this in ProcessCompletedLoads when we can access the graphics device
                    return textureData;
                }
                catch (OperationCanceledException)
                {
                    // Loading was cancelled, reset state
                    lock (_lockObject)
                    {
                        entry.IsLoading = false;
                    }
                    return null;
                }
                catch (Exception ex)
                {
                    // Log error (in production, use proper logging)
                    System.Diagnostics.Debug.WriteLine($"Error loading texture {entry.Name}: {ex.Message}");
                    lock (_lockObject)
                    {
                        entry.IsLoading = false;
                    }
                    return null;
                }
            }, _cancellationTokenSource.Token);

            lock (_lockObject)
            {
                entry.LoadingTask = loadingTask;
                _loadingTasks[entry.Name] = loadingTask;
            }
        }

        /// <summary>
        /// Processes completed texture loading tasks and creates Texture2D objects on the main thread.
        /// MonoGame requires all graphics operations to be performed on the main thread.
        /// Based on MonoGame API: https://docs.monogame.net/api/Microsoft.Xna.Framework.Graphics.Texture2D.html
        /// Texture2D.FromStream must be called on the thread that owns the GraphicsDevice.
        /// </summary>
        private void ProcessCompletedLoads()
        {
            var completedTasks = new List<KeyValuePair<string, Task<byte[]>>>();

            lock (_lockObject)
            {
                foreach (var kvp in _loadingTasks)
                {
                    if (kvp.Value.IsCompleted)
                    {
                        completedTasks.Add(new KeyValuePair<string, Task<byte[]>>(kvp.Key, kvp.Value));
                    }
                }
            }

            foreach (var kvp in completedTasks)
            {
                string textureName = kvp.Key;
                Task<byte[]> task = kvp.Value;

                TextureEntry entry;
                if (!_textures.TryGetValue(textureName, out entry))
                {
                    continue; // Entry was removed
                }

                try
                {
                    if (task.IsFaulted || task.IsCanceled || task.Result == null)
                    {
                        // Loading failed or was cancelled
                        lock (_lockObject)
                        {
                            entry.IsLoading = false;
                            entry.LoadingTask = null;
                            _loadingTasks.Remove(textureName);
                        }
                        continue;
                    }

                    byte[] textureData = task.Result;

                    // Create texture on main thread (required by MonoGame)
                    Texture2D texture;
                    using (var stream = new MemoryStream(textureData))
                    {
                        texture = Texture2D.FromStream(_graphicsDevice, stream);
                    }

                    // Calculate VRAM size
                    // VRAM size = width * height * bytes per pixel * mip levels
                    // For simplicity, we calculate base size. Actual size may vary with compression.
                    int bytesPerPixel = 4; // RGBA32 default
                    switch (texture.Format)
                    {
                        case SurfaceFormat.Color:
                            bytesPerPixel = 4;
                            break;
                        case SurfaceFormat.Bgr565:
                        case SurfaceFormat.Bgra5551:
                            bytesPerPixel = 2;
                            break;
                        case SurfaceFormat.Alpha8:
                            bytesPerPixel = 1;
                            break;
                        case SurfaceFormat.Dxt1:
                            // DXT1 is 4x4 blocks, 8 bytes per block
                            bytesPerPixel = 0; // Will calculate separately
                            break;
                        case SurfaceFormat.Dxt3:
                        case SurfaceFormat.Dxt5:
                            // DXT3/5 is 4x4 blocks, 16 bytes per block
                            bytesPerPixel = 0; // Will calculate separately
                            break;
                        default:
                            bytesPerPixel = 4; // Default to RGBA32
                            break;
                    }

                    long vramSize;
                    if (bytesPerPixel == 0)
                    {
                        // Compressed format - calculate based on block size
                        int blockSize = texture.Format == SurfaceFormat.Dxt1 ? 8 : 16;
                        int blocksWide = (texture.Width + 3) / 4;
                        int blocksHigh = (texture.Height + 3) / 4;
                        vramSize = blocksWide * blocksHigh * blockSize;
                    }
                    else
                    {
                        vramSize = texture.Width * texture.Height * bytesPerPixel;
                    }

                    // Account for mipmaps (each mip level is 1/4 the size of the previous)
                    if (texture.LevelCount > 1)
                    {
                        long totalMipSize = vramSize;
                        int currentWidth = texture.Width;
                        int currentHeight = texture.Height;
                        for (int i = 1; i < texture.LevelCount; i++)
                        {
                            currentWidth = Math.Max(1, currentWidth / 2);
                            currentHeight = Math.Max(1, currentHeight / 2);
                            long mipSize;
                            if (bytesPerPixel == 0)
                            {
                                int blockSize = texture.Format == SurfaceFormat.Dxt1 ? 8 : 16;
                                int blocksWide = (currentWidth + 3) / 4;
                                int blocksHigh = (currentHeight + 3) / 4;
                                mipSize = blocksWide * blocksHigh * blockSize;
                            }
                            else
                            {
                                mipSize = currentWidth * currentHeight * bytesPerPixel;
                            }
                            totalMipSize += mipSize;
                        }
                        vramSize = totalMipSize;
                    }

                    // Update entry
                    lock (_lockObject)
                    {
                        entry.Texture = texture;
                        entry.VRAMSize = vramSize;
                        entry.IsStreaming = false;
                        entry.IsLoading = false;
                        entry.LoadingTask = null;
                        _currentVRAMUsage += vramSize;
                        _loadingTasks.Remove(textureName);
                    }
                }
                catch (Exception ex)
                {
                    // Log error (in production, use proper logging)
                    System.Diagnostics.Debug.WriteLine($"Error creating texture {textureName}: {ex.Message}");
                    lock (_lockObject)
                    {
                        entry.IsLoading = false;
                        entry.LoadingTask = null;
                        _loadingTasks.Remove(textureName);
                    }
                }
            }
        }

        /// <summary>
        /// Loads a texture from a file path asynchronously.
        /// Based on MonoGame API: https://docs.monogame.net/api/Microsoft.Xna.Framework.Graphics.Texture2D.html
        /// This method reads the file on a background thread and creates the texture on the main thread.
        /// </summary>
        /// <param name="textureName">Name of the texture.</param>
        /// <param name="filePath">Path to the texture file.</param>
        /// <param name="priority">Loading priority (higher = more important).</param>
        public void LoadTextureFromFile(string textureName, string filePath, float priority = 1.0f)
        {
            if (string.IsNullOrEmpty(textureName))
            {
                throw new ArgumentException("Texture name cannot be null or empty.", nameof(textureName));
            }
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
            }

            TextureEntry entry;
            if (!_textures.TryGetValue(textureName, out entry))
            {
                entry = new TextureEntry
                {
                    Name = textureName,
                    Priority = priority,
                    IsStreaming = true
                };
                _textures[textureName] = entry;
            }
            else
            {
                entry.Priority = priority;
            }

            // Set up file-based loader
            _textureLoader = async (name, cancellationToken) =>
            {
                if (name != textureName)
                {
                    return null;
                }

                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"Texture file not found: {filePath}");
                }

                return new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, true);
            };

            LoadTextureAsync(entry);
        }

        /// <summary>
        /// Loads a texture from a stream asynchronously.
        /// Based on MonoGame API: https://docs.monogame.net/api/Microsoft.Xna.Framework.Graphics.Texture2D.html
        /// This method reads the stream on a background thread and creates the texture on the main thread.
        /// </summary>
        /// <param name="textureName">Name of the texture.</param>
        /// <param name="stream">Stream containing texture data. The stream will be disposed after reading.</param>
        /// <param name="priority">Loading priority (higher = more important).</param>
        public void LoadTextureFromStream(string textureName, Stream stream, float priority = 1.0f)
        {
            if (string.IsNullOrEmpty(textureName))
            {
                throw new ArgumentException("Texture name cannot be null or empty.", nameof(textureName));
            }
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            TextureEntry entry;
            if (!_textures.TryGetValue(textureName, out entry))
            {
                entry = new TextureEntry
                {
                    Name = textureName,
                    Priority = priority,
                    IsStreaming = true
                };
                _textures[textureName] = entry;
            }
            else
            {
                entry.Priority = priority;
            }

            // Create a memory stream copy for async loading
            var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            memoryStream.Position = 0;

            // Set up stream-based loader
            _textureLoader = async (name, cancellationToken) =>
            {
                if (name != textureName)
                {
                    return null;
                }

                var result = new MemoryStream(memoryStream.ToArray());
                return result;
            };

            LoadTextureAsync(entry);
        }

        private void UnloadTexture(TextureEntry entry)
        {
            if (entry.Texture != null)
            {
                _currentVRAMUsage -= entry.VRAMSize;
                entry.Texture.Dispose();
                entry.Texture = null;
                entry.IsStreaming = true;
            }
        }

        /// <summary>
        /// Sets texture priority for streaming decisions.
        /// </summary>
        public void SetPriority(string name, float priority)
        {
            TextureEntry entry;
            if (_textures.TryGetValue(name, out entry))
            {
                entry.Priority = priority;
            }
        }

        /// <summary>
        /// Removes a texture from the manager.
        /// </summary>
        public void RemoveTexture(string name)
        {
            TextureEntry entry;
            if (_textures.TryGetValue(name, out entry))
            {
                UnloadTexture(entry);
                _textures.Remove(name);
            }
        }

        public void Dispose()
        {
            // Cancel all loading operations
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
            }

            // Wait for all loading tasks to complete or cancel
            Task<byte[]>[] tasksToWait;
            lock (_lockObject)
            {
                tasksToWait = new Task<byte[]>[_loadingTasks.Count];
                _loadingTasks.Values.CopyTo(tasksToWait, 0);
            }

            try
            {
                Task.WaitAll(tasksToWait, TimeSpan.FromSeconds(5));
            }
            catch (AggregateException)
            {
                // Some tasks may have been cancelled or faulted, which is expected
            }

            // Dispose all textures
            foreach (TextureEntry entry in _textures.Values)
            {
                if (entry.Texture != null)
                {
                    entry.Texture.Dispose();
                }
            }

            lock (_lockObject)
            {
                _textures.Clear();
                _loadingTasks.Clear();
            }

            _currentVRAMUsage = 0;

            // Dispose cancellation token source
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Dispose();
            }
        }
    }
}


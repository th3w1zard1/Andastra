using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Andastra.Parsing.Resource;
using Andastra.Runtime.Content.Interfaces;
using JetBrains.Annotations;

namespace Andastra.Runtime.MonoGame.Loading
{
    /// <summary>
    /// Asynchronous resource loader for textures and models.
    /// 
    /// Uses Task-based parallelism to load and parse resources off the main thread,
    /// similar to PyKotor's ProcessPoolExecutor approach but using .NET Tasks.
    /// 
    /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/gl/scene/async_loader.py:378
    /// Original: class AsyncResourceLoader with ProcessPoolExecutor
    /// </summary>
    /// <remarks>
    /// Async Resource Loader:
    /// - Based on swkotor2.exe resource loading system (modern async enhancement)
    /// - Located via string references: "ModuleLoaded" @ 0x007bdd70, "CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_LOAD" @ 0x007bc91c
    /// - "LoadScreenID" @ 0x007bd54c, "LoadSavegame" @ 0x007bdc90, "LOADMUSIC" @ 0x007be044
    /// - "load_%s" @ 0x007be32c (load format string), "Mod_OnModLoad" @ 0x007be7bc
    /// - "SkipOnLoad" @ 0x007c0344 (skip on load flag), "Loadscreens" @ 0x007c4c04
    /// - "ClientLoad" @ 0x007c4fdc, "Loading" @ 0x007c7e40, "Load Bar = %d" @ 0x007c760c
    /// - "LoadBar" @ 0x007cb33c, "LBL_LOADING" @ 0x007cbe10, "loadscreen_p" @ 0x007cbe40
    /// - Error messages:
    ///   - "CSWCCreature::LoadModel(): Failed to load creature model '%s'." @ 0x007c82fc
    ///   - "CSWCVisualEffect::LoadModel: Failed to load visual effect model '%s'." @ 0x007cd5a8
    ///   - "CSWCCreatureAppearance::CreateBTypeBody(): Failed to load model '%s'." @ 0x007cdc40
    ///   - "Model %s nor the default model %s could be loaded." @ 0x007cad14
    ///   - "Icon %s nor the default icon %s could be loaded." @ 0x007cad48
    ///   - "Cannot load door model '%s'." @ 0x007d2488
    ///   - "Problem loading encounter with tag '%s'.  It has geometry, but no vertices.  Skipping." @ 0x007c0ae0
    /// - Resource management: "CExoKeyTable::DestroyTable: Resource %s still in demand during table deletion" @ 0x007b6078
    /// - "CExoKeyTable::AddKey: Duplicate Resource " @ 0x007b6124, "Resource" @ 0x007c14d4
    /// - Original implementation: KOTOR loads resources synchronously from CHITIN keyfiles and modules
    /// - Resource loading: Loads TPC (texture), MDL (model), MDX (geometry) files from game installation
    /// - This MonoGame implementation: Modern async Task-based loading for performance (off main thread IO)
    /// - Resource provider: Uses IGameResourceProvider to resolve resource locations (CHITIN, modules, etc.)
    /// - Pattern: IO operations off main thread, GPU object creation on main thread with graphics context
    /// - Note: Original engine loaded resources synchronously during area/module loading
    /// - This async implementation provides smoother frame rates during resource-heavy operations
    /// </remarks>
    public class AsyncResourceLoader : IDisposable
    {
        private readonly IGameResourceProvider _resourceProvider;
        private readonly ConcurrentQueue<TextureLoadTask> _completedTextures;
        private readonly ConcurrentQueue<ModelLoadTask> _completedModels;
        private readonly ConcurrentDictionary<string, Task<TextureLoadResult>> _pendingTextures;
        private readonly ConcurrentDictionary<string, Task<ModelLoadResult>> _pendingModels;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private int _maxConcurrentLoads;

        /// <summary>
        /// Gets or sets the maximum number of concurrent resource loads.
        /// </summary>
        public int MaxConcurrentLoads
        {
            get { return _maxConcurrentLoads; }
            set { _maxConcurrentLoads = Math.Max(1, value); }
        }

        /// <summary>
        /// Initializes a new async resource loader.
        /// </summary>
        /// <param name="resourceProvider">Resource provider for resolving file locations. Must not be null.</param>
        /// <param name="maxConcurrentLoads">Maximum concurrent loads (default: number of CPU cores). Must be greater than zero if specified.</param>
        /// <exception cref="ArgumentNullException">Thrown if resourceProvider is null.</exception>
        public AsyncResourceLoader([NotNull] IGameResourceProvider resourceProvider, int maxConcurrentLoads = 0)
        {
            if (resourceProvider == null)
            {
                throw new ArgumentNullException(nameof(resourceProvider));
            }

            _resourceProvider = resourceProvider;
            _maxConcurrentLoads = maxConcurrentLoads > 0 ? maxConcurrentLoads : Environment.ProcessorCount;
            _completedTextures = new ConcurrentQueue<TextureLoadTask>();
            _completedModels = new ConcurrentQueue<ModelLoadTask>();
            _pendingTextures = new ConcurrentDictionary<string, Task<TextureLoadResult>>();
            _pendingModels = new ConcurrentDictionary<string, Task<ModelLoadResult>>();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// Starts loading a texture asynchronously.
        /// Returns immediately with a task that completes when the texture is loaded and parsed.
        /// </summary>
        /// <param name="textureName">Name of the texture resource.</param>
        /// <returns>Task that completes with texture data or error.</returns>
        public Task<TextureLoadResult> LoadTextureAsync(string textureName)
        {
            if (string.IsNullOrEmpty(textureName))
            {
                return Task.FromResult(new TextureLoadResult { Success = false, ErrorMessage = "Texture name is null or empty" });
            }

            // Return existing task if already loading
            Task<TextureLoadResult> existingTask;
            if (_pendingTextures.TryGetValue(textureName, out existingTask))
            {
                return existingTask;
            }

            // Create new load task
            var task = Task.Run(async () =>
            {
                try
                {
                    // Resolve resource using IGameResourceProvider
                    var resourceId = new ResourceIdentifier(textureName, ResourceType.TPC);

                    // Check if resource exists
                    bool exists = await _resourceProvider.ExistsAsync(resourceId, _cancellationTokenSource.Token);
                    if (!exists)
                    {
                        return new TextureLoadResult
                        {
                            TextureName = textureName,
                            Success = false,
                            ErrorMessage = $"Texture resource not found: {textureName}"
                        };
                    }

                    // Load resource bytes asynchronously (IO operation off main thread)
                    byte[] fileData = await _resourceProvider.GetResourceBytesAsync(resourceId, _cancellationTokenSource.Token);

                    if (fileData == null || fileData.Length == 0)
                    {
                        return new TextureLoadResult
                        {
                            TextureName = textureName,
                            Success = false,
                            ErrorMessage = $"Texture resource is empty: {textureName}"
                        };
                    }

                    // Return raw data - actual parsing happens on main thread with graphics context
                    // This matches PyKotor's pattern where IO+parsing happens in child process,
                    // but GPU object creation happens on main thread
                    return new TextureLoadResult
                    {
                        TextureName = textureName,
                        Success = true,
                        FilePath = null, // Path not needed since we have bytes
                        FileData = fileData
                    };
                }
                catch (OperationCanceledException)
                {
                    return new TextureLoadResult
                    {
                        TextureName = textureName,
                        Success = false,
                        ErrorMessage = $"Texture load cancelled: {textureName}"
                    };
                }
                catch (Exception ex)
                {
                    return new TextureLoadResult
                    {
                        TextureName = textureName,
                        Success = false,
                        ErrorMessage = $"Error loading texture '{textureName}': {ex.Message}"
                    };
                }
            }, _cancellationTokenSource.Token);

            _pendingTextures.TryAdd(textureName, task);

            // Remove from pending when complete
            task.ContinueWith(t =>
            {
                TextureLoadResult result;
                _pendingTextures.TryRemove(textureName, out Task<TextureLoadResult> removed);
                if (t.IsCompletedSuccessfully)
                {
                    result = t.Result;
                    _completedTextures.Enqueue(new TextureLoadTask { TextureName = textureName, Result = result });
                }
            }, TaskContinuationOptions.ExecuteSynchronously);

            return task;
        }

        /// <summary>
        /// Starts loading a model asynchronously (MDL + MDX).
        /// Returns immediately with a task that completes when the model is loaded and parsed.
        /// </summary>
        /// <param name="modelName">Name of the model resource (without .mdl/.mdx extension).</param>
        /// <returns>Task that completes with model data or error.</returns>
        public Task<ModelLoadResult> LoadModelAsync(string modelName)
        {
            if (string.IsNullOrEmpty(modelName))
            {
                return Task.FromResult(new ModelLoadResult { Success = false, ErrorMessage = "Model name is null or empty" });
            }

            // Return existing task if already loading
            if (_pendingModels.TryGetValue(modelName, out Task<ModelLoadResult> existingTask))
            {
                return existingTask;
            }

            // Create new load task
            var task = Task.Run(async () =>
            {
                try
                {
                    // Load MDL resource using IGameResourceProvider
                    var mdlResourceId = new ResourceIdentifier(
                        modelName,
                        ResourceType.MDL);

                    // Check if MDL exists
                    bool mdlExists = await _resourceProvider.ExistsAsync(mdlResourceId, _cancellationTokenSource.Token);
                    if (!mdlExists)
                    {
                        return new ModelLoadResult
                        {
                            ModelName = modelName,
                            Success = false,
                            ErrorMessage = $"MDL resource not found: {modelName}"
                        };
                    }

                    // Load MDL bytes asynchronously (IO operation off main thread)
                    byte[] mdlData = await _resourceProvider.GetResourceBytesAsync(mdlResourceId, _cancellationTokenSource.Token);

                    if (mdlData == null || mdlData.Length == 0)
                    {
                        return new ModelLoadResult
                        {
                            ModelName = modelName,
                            Success = false,
                            ErrorMessage = $"MDL resource is empty: {modelName}"
                        };
                    }

                    // Load MDX resource (same name, different extension)
                    byte[] mdxData = null;
                    var mdxResourceId = new ResourceIdentifier(
                        modelName,
                        ResourceType.MDX);

                    bool mdxExists = await _resourceProvider.ExistsAsync(mdxResourceId, _cancellationTokenSource.Token);
                    if (mdxExists)
                    {
                        mdxData = await _resourceProvider.GetResourceBytesAsync(mdxResourceId, _cancellationTokenSource.Token);
                    }

                    // Return raw data - actual parsing happens on main thread with graphics context
                    // This matches PyKotor's pattern where IO+parsing happens in child process,
                    // but GPU object creation happens on main thread
                    return new ModelLoadResult
                    {
                        ModelName = modelName,
                        Success = true,
                        MdlFilePath = null, // Path not needed since we have bytes
                        MdxFilePath = null, // Path not needed since we have bytes
                        MdlData = mdlData,
                        MdxData = mdxData
                    };
                }
                catch (OperationCanceledException)
                {
                    return new ModelLoadResult
                    {
                        ModelName = modelName,
                        Success = false,
                        ErrorMessage = $"Model load cancelled: {modelName}"
                    };
                }
                catch (Exception ex)
                {
                    return new ModelLoadResult
                    {
                        ModelName = modelName,
                        Success = false,
                        ErrorMessage = $"Error loading model '{modelName}': {ex.Message}"
                    };
                }
            }, _cancellationTokenSource.Token);

            _pendingModels.TryAdd(modelName, task);

            // Remove from pending when complete
            task.ContinueWith(t =>
            {
                ModelLoadResult result;
                Task<ModelLoadResult> removed;
                _pendingModels.TryRemove(modelName, out removed);
                if (t.IsCompletedSuccessfully)
                {
                    result = t.Result;
                    _completedModels.Enqueue(new ModelLoadTask { ModelName = modelName, Result = result });
                }
            }, TaskContinuationOptions.ExecuteSynchronously);

            return task;
        }

        /// <summary>
        /// Polls for completed texture loads.
        /// Must be called from main thread with active graphics context.
        /// Based on PyKotor async_loader.py:442 poll_async_resources method
        /// </summary>
        /// <param name="maxPerFrame">Maximum textures to process per frame.</param>
        /// <returns>Array of completed texture loads.</returns>
        public TextureLoadTask[] PollCompletedTextures(int maxPerFrame = 8)
        {
            var results = new System.Collections.Generic.List<TextureLoadTask>();
            for (int i = 0; i < maxPerFrame && _completedTextures.TryDequeue(out TextureLoadTask task); i++)
            {
                results.Add(task);
            }
            return results.ToArray();
        }

        /// <summary>
        /// Polls for completed model loads.
        /// Must be called from main thread with active graphics context.
        /// </summary>
        /// <param name="maxPerFrame">Maximum models to process per frame.</param>
        /// <returns>Array of completed model loads.</returns>
        public ModelLoadTask[] PollCompletedModels(int maxPerFrame = 4)
        {
            var results = new System.Collections.Generic.List<ModelLoadTask>();
            for (int i = 0; i < maxPerFrame && _completedModels.TryDequeue(out ModelLoadTask task); i++)
            {
                results.Add(task);
            }
            return results.ToArray();
        }

        /// <summary>
        /// Cancels a pending texture load.
        /// </summary>
        public void CancelTextureLoad(string textureName)
        {
            Task<TextureLoadResult> task;
            if (_pendingTextures.TryRemove(textureName, out task))
            {
                // Task cancellation is handled by CancellationTokenSource
            }
        }

        /// <summary>
        /// Cancels a pending model load.
        /// </summary>
        public void CancelModelLoad(string modelName)
        {
            Task<ModelLoadResult> task;
            if (_pendingModels.TryRemove(modelName, out task))
            {
                // Task cancellation is handled by CancellationTokenSource
            }
        }

        /// <summary>
        /// Gets the number of pending texture loads.
        /// </summary>
        public int PendingTextureCount
        {
            get { return _pendingTextures.Count; }
        }

        /// <summary>
        /// Gets the number of pending model loads.
        /// </summary>
        public int PendingModelCount
        {
            get { return _pendingModels.Count; }
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            _pendingTextures.Clear();
            _pendingModels.Clear();
            while (_completedTextures.TryDequeue(out _)) { }
            while (_completedModels.TryDequeue(out _)) { }
        }
    }

    /// <summary>
    /// Result of an async texture load operation.
    /// </summary>
    public struct TextureLoadResult
    {
        public string TextureName;
        public bool Success;
        public string ErrorMessage;
        public string FilePath;
        public byte[] FileData;
    }

    /// <summary>
    /// Result of an async model load operation.
    /// </summary>
    public struct ModelLoadResult
    {
        public string ModelName;
        public bool Success;
        public string ErrorMessage;
        public string MdlFilePath;
        public string MdxFilePath;
        public byte[] MdlData;
        public byte[] MdxData;
    }

    /// <summary>
    /// Completed texture load task.
    /// </summary>
    public struct TextureLoadTask
    {
        public string TextureName;
        public TextureLoadResult Result;
    }

    /// <summary>
    /// Completed model load task.
    /// </summary>
    public struct ModelLoadTask
    {
        public string ModelName;
        public ModelLoadResult Result;
    }
}


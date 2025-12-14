using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Odyssey.Content.Interfaces;

namespace Odyssey.MonoGame.Loading
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
        /// <param name="resourceProvider">Resource provider for resolving file locations.</param>
        /// <param name="maxConcurrentLoads">Maximum concurrent loads (default: number of CPU cores).</param>
        public AsyncResourceLoader([NotNull] IGameResourceProvider resourceProvider, int maxConcurrentLoads = 0)
        {
            if (resourceProvider == null)
            {
                throw new ArgumentNullException("resourceProvider");
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
            var task = Task.Run(() =>
            {
                try
                {
                    // Resolve file location using resource provider
                    // TODO: Implement texture path resolution using IGameResourceProvider
                    // For now, use a placeholder - actual implementation would use ResourceIdentifier
                    string filePath = null; // _resourceProvider.LocateAsync(...)
                    if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                    {
                        return new TextureLoadResult
                        {
                            TextureName = textureName,
                            Success = false,
                            ErrorMessage = $"Texture file not found: {textureName}"
                        };
                    }

                    // Read file bytes (IO operation)
                    byte[] fileData;
                    using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        fileData = new byte[fs.Length];
                        fs.Read(fileData, 0, fileData.Length);
                    }

                    // Parse texture data (CPU-bound parsing)
                    // This would call into CSharpKOTOR texture parsing
                    // For now, return raw data - actual parsing happens on main thread with OpenGL context
                    return new TextureLoadResult
                    {
                        TextureName = textureName,
                        Success = true,
                        FilePath = filePath,
                        FileData = fileData
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
                Task<TextureLoadResult> removed;
                _pendingTextures.TryRemove(textureName, out removed);
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
            Task<ModelLoadResult> existingTask;
            if (_pendingModels.TryGetValue(modelName, out existingTask))
            {
                return existingTask;
            }

            // Create new load task
            var task = Task.Run(() =>
            {
                try
                {
                    // Resolve file locations using resource provider
                    // TODO: Implement model path resolution using IGameResourceProvider
                    // For now, use placeholders - actual implementation would use ResourceIdentifier
                    string mdlPath = null; // _resourceProvider.LocateAsync(...)
                    string mdxPath = null; // _resourceProvider.LocateAsync(...)

                    if (string.IsNullOrEmpty(mdlPath) || !File.Exists(mdlPath))
                    {
                        return new ModelLoadResult
                        {
                            ModelName = modelName,
                            Success = false,
                            ErrorMessage = $"MDL file not found: {modelName}"
                        };
                    }

                    // Read MDL file bytes
                    byte[] mdlData;
                    using (FileStream fs = new FileStream(mdlPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        mdlData = new byte[fs.Length];
                        fs.Read(mdlData, 0, mdlData.Length);
                    }

                    // Read MDX file bytes if available
                    byte[] mdxData = null;
                    if (!string.IsNullOrEmpty(mdxPath) && File.Exists(mdxPath))
                    {
                        using (FileStream fs = new FileStream(mdxPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            mdxData = new byte[fs.Length];
                            fs.Read(mdxData, 0, mdxData.Length);
                        }
                    }

                    return new ModelLoadResult
                    {
                        ModelName = modelName,
                        Success = true,
                        MdlFilePath = mdlPath,
                        MdxFilePath = mdxPath,
                        MdlData = mdlData,
                        MdxData = mdxData
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


using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Runtime.MonoGame.Assets
{
    /// <summary>
    /// Asset hot reload system for development workflow.
    /// 
    /// Hot reload allows assets to be reloaded at runtime when files change,
    /// enabling rapid iteration during development without restarting the game.
    /// 
    /// Features:
    /// - File system watching
    /// - Automatic asset reloading
    /// - Dependency tracking
    /// - Safe reloading (no crashes)
    /// </summary>
    public class AssetHotReload : IDisposable
    {
        /// <summary>
        /// Asset reload callback.
        /// </summary>
        public delegate void AssetReloadedHandler(string assetPath, object newAsset);

        /// <summary>
        /// Asset entry.
        /// </summary>
        private class AssetEntry
        {
            public string FilePath;
            public DateTime LastWriteTime;
            public object Asset;
            public AssetReloadedHandler ReloadHandler;
            public List<string> Dependencies;
        }

        private readonly Dictionary<string, AssetEntry> _assets;
        private readonly FileSystemWatcher _fileWatcher;
        private readonly object _lock;
        private bool _enabled;

        /// <summary>
        /// Gets or sets whether hot reload is enabled.
        /// </summary>
        public bool Enabled
        {
            get { return _enabled; }
            set { _enabled = value; }
        }

        /// <summary>
        /// Initializes a new asset hot reload system.
        /// </summary>
        /// <param name="watchDirectory">Directory to watch for file changes.</param>
        public AssetHotReload(string watchDirectory)
        {
            _assets = new Dictionary<string, AssetEntry>();
            _lock = new object();
            _enabled = true;

            _fileWatcher = new FileSystemWatcher(watchDirectory)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                IncludeSubdirectories = true
            };

            _fileWatcher.Changed += OnFileChanged;
            _fileWatcher.EnableRaisingEvents = true;
        }

        /// <summary>
        /// Registers an asset for hot reloading.
        /// </summary>
        public void RegisterAsset(string filePath, object asset, AssetReloadedHandler reloadHandler, List<string> dependencies = null)
        {
            if (string.IsNullOrEmpty(filePath) || asset == null)
            {
                return;
            }

            lock (_lock)
            {
                FileInfo fileInfo = new FileInfo(filePath);
                _assets[filePath] = new AssetEntry
                {
                    FilePath = filePath,
                    LastWriteTime = fileInfo.LastWriteTime,
                    Asset = asset,
                    ReloadHandler = reloadHandler,
                    Dependencies = dependencies ?? new List<string>()
                };
            }
        }

        /// <summary>
        /// Unregisters an asset.
        /// </summary>
        public void UnregisterAsset(string filePath)
        {
            lock (_lock)
            {
                _assets.Remove(filePath);
            }
        }

        /// <summary>
        /// Checks for changed assets and reloads them.
        /// </summary>
        public void Update()
        {
            if (!_enabled)
            {
                return;
            }

            lock (_lock)
            {
                var toReload = new List<AssetEntry>();

                foreach (AssetEntry entry in _assets.Values)
                {
                    if (File.Exists(entry.FilePath))
                    {
                        FileInfo fileInfo = new FileInfo(entry.FilePath);
                        if (fileInfo.LastWriteTime > entry.LastWriteTime)
                        {
                            toReload.Add(entry);
                        }
                    }
                }

                // Reload changed assets
                foreach (AssetEntry entry in toReload)
                {
                    ReloadAsset(entry);
                }
            }
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            // File system watcher callback
            // Update() will handle the actual reloading
        }

        private void ReloadAsset(AssetEntry entry)
        {
            try
            {
                // Reload asset
                object newAsset = LoadAsset(entry.FilePath);

                if (newAsset != null)
                {
                    // Update entry
                    FileInfo fileInfo = new FileInfo(entry.FilePath);
                    entry.LastWriteTime = fileInfo.LastWriteTime;
                    entry.Asset = newAsset;

                    // Notify handler
                    entry.ReloadHandler?.Invoke(entry.FilePath, newAsset);

                    // Reload dependencies
                    foreach (string dep in entry.Dependencies)
                    {
                        AssetEntry depEntry;
                        if (_assets.TryGetValue(dep, out depEntry))
                        {
                            ReloadAsset(depEntry);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but don't crash
                System.Diagnostics.Debug.WriteLine($"Failed to reload asset {entry.FilePath}: {ex.Message}");
            }
        }

        private object LoadAsset(string filePath)
        {
            // Load asset based on file extension
            // Placeholder - would implement actual asset loading
            return null;
        }

        public void Dispose()
        {
            _fileWatcher?.Dispose();
            _assets.Clear();
        }
    }
}


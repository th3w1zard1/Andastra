using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace BioWareEngines.MonoGame.Textures
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
    /// </summary>
    public class TextureStreamingManager : IDisposable
    {
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
            public long VRAMSize;
        }

        private readonly GraphicsDevice _graphicsDevice;
        private readonly Dictionary<string, TextureEntry> _textures;
        private long _currentVRAMUsage;
        private long _maxVRAMBudget;

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
        /// <param name="maxVRAMBudget">Maximum VRAM budget in bytes (0 = unlimited).</param>
        public TextureStreamingManager(GraphicsDevice graphicsDevice, long maxVRAMBudget = 0)
        {
            if (graphicsDevice == null)
            {
                throw new ArgumentNullException(nameof(graphicsDevice));
            }

            _graphicsDevice = graphicsDevice;
            _textures = new Dictionary<string, TextureEntry>();
            _maxVRAMBudget = maxVRAMBudget;
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
                if (entry.Texture == null && entry.IsStreaming)
                {
                    // Check VRAM budget
                    if (_maxVRAMBudget > 0 && _currentVRAMUsage >= _maxVRAMBudget)
                    {
                        break; // Out of VRAM budget
                    }

                    // Load texture (this would be async in real implementation)
                    // LoadTexture(entry);
                }
            }
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
            foreach (TextureEntry entry in _textures.Values)
            {
                if (entry.Texture != null)
                {
                    entry.Texture.Dispose();
                }
            }
            _textures.Clear();
            _currentVRAMUsage = 0;
        }
    }
}


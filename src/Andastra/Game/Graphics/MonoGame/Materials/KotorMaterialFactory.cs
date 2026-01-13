using System;
using System.Collections.Generic;
using BioWare.NET.Resource.Formats.TPC;
using Andastra.Runtime.MonoGame.Converters;
using Andastra.Runtime.MonoGame.Enums;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Game.Graphics.MonoGame.Materials
{
    /// <summary>
    /// Factory for creating MonoGame materials from KOTOR texture and material data.
    /// Supports opaque, alpha cutout, alpha blend, additive, and lightmapped materials.
    /// </summary>
    /// <remarks>
    /// In MonoGame, materials are represented using BasicEffect or custom Effect classes.
    /// This factory creates texture references and material metadata that can be used
    /// with MonoGame's rendering pipeline.
    /// </remarks>
    public class KotorMaterialFactory
    {
        private readonly GraphicsDevice _device;
        private readonly Dictionary<string, MaterialData> _materialCache;
        private readonly Dictionary<string, Texture2D> _textureCache;

        /// <summary>
        /// Material data structure for MonoGame rendering.
        /// </summary>
        public class MaterialData
        {
            public Texture2D DiffuseTexture { get; set; }
            public Texture2D LightmapTexture { get; set; }
            public MaterialType Type { get; set; }
            public float AlphaThreshold { get; set; } = 0.5f;
            public Color DiffuseColor { get; set; } = Color.White;
            public BlendState BlendState { get; set; }
            public bool IsTransparent { get; set; }
        }

        /// <summary>
        /// Creates a new material factory.
        /// </summary>
        /// <param name="device">Graphics device.</param>
        // Initialize material factory with graphics device
        // Based on MonoGame API: https://docs.monogame.net/api/Microsoft.Xna.Framework.Graphics.GraphicsDevice.html
        // GraphicsDevice provides access to graphics hardware and resources
        // Texture2D is MonoGame's texture type
        // Source: https://docs.monogame.net/articles/getting_to_know/howto/graphics/HowTo_Load_Texture.html
        public KotorMaterialFactory([NotNull] GraphicsDevice device)
        {
            _device = device ?? throw new ArgumentNullException("device");
            _materialCache = new Dictionary<string, MaterialData>(StringComparer.OrdinalIgnoreCase);
            _textureCache = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Creates or retrieves a material for the given diffuse texture.
        /// </summary>
        /// <param name="diffuseTexName">Diffuse texture name.</param>
        /// <param name="lightmapTexName">Optional lightmap texture name.</param>
        /// <param name="loadTexture">Function to load TPC by name.</param>
        /// <returns>A MonoGame MaterialData.</returns>
        public MaterialData GetMaterial(
            string diffuseTexName,
            string lightmapTexName,
            Func<string, TPC> loadTexture)
        {
            // Create cache key
            string key = (diffuseTexName ?? "default") + "_" + (lightmapTexName ?? "none");

            // Check cache
            if (_materialCache.TryGetValue(key, out MaterialData cached))
            {
                return cached;
            }

            // Create new material
            MaterialData material = CreateMaterial(diffuseTexName, lightmapTexName, loadTexture);
            _materialCache[key] = material;

            return material;
        }

        /// <summary>
        /// Creates an opaque material with diffuse texture.
        /// </summary>
        public MaterialData CreateOpaqueMaterial(string diffuseTexName, Func<string, TPC> loadTexture)
        {
            // Load diffuse texture
            Texture2D diffuseTex = LoadTexture(diffuseTexName, loadTexture);

            var material = new MaterialData
            {
                DiffuseTexture = diffuseTex ?? CreateDefaultTexture(),
                Type = MaterialType.Opaque,
                BlendState = BlendState.Opaque,
                IsTransparent = false
            };

            return material;
        }

        /// <summary>
        /// Creates an alpha cutout material for transparency with hard edges.
        /// </summary>
        public MaterialData CreateAlphaCutoutMaterial(string diffuseTexName, Func<string, TPC> loadTexture, float alphaThreshold = 0.5f)
        {
            Texture2D diffuseTex = LoadTexture(diffuseTexName, loadTexture);

            var material = new MaterialData
            {
                DiffuseTexture = diffuseTex ?? CreateDefaultTexture(),
                Type = MaterialType.AlphaCutout,
                AlphaThreshold = alphaThreshold,
                BlendState = BlendState.Opaque, // Alpha testing handled in shader
                IsTransparent = true
            };

            return material;
        }

        /// <summary>
        /// Creates an alpha blend material for soft transparency.
        /// </summary>
        public MaterialData CreateAlphaBlendMaterial(string diffuseTexName, Func<string, TPC> loadTexture)
        {
            Texture2D diffuseTex = LoadTexture(diffuseTexName, loadTexture);

            var material = new MaterialData
            {
                DiffuseTexture = diffuseTex ?? CreateDefaultTexture(),
                Type = MaterialType.AlphaBlend,
                BlendState = BlendState.AlphaBlend,
                IsTransparent = true
            };

            return material;
        }

        /// <summary>
        /// Creates an additive blend material for glowing/emissive effects.
        /// </summary>
        public MaterialData CreateAdditiveMaterial(string diffuseTexName, Func<string, TPC> loadTexture)
        {
            Texture2D diffuseTex = LoadTexture(diffuseTexName, loadTexture);

            var material = new MaterialData
            {
                DiffuseTexture = diffuseTex ?? CreateDefaultTexture(),
                Type = MaterialType.Additive,
                BlendState = BlendState.Additive,
                IsTransparent = true
            };

            return material;
        }

        /// <summary>
        /// Creates a lightmapped material with baked lighting.
        /// </summary>
        public MaterialData CreateLightmappedMaterial(string diffuseTexName, string lightmapTexName, Func<string, TPC> loadTexture)
        {
            Texture2D diffuseTex = LoadTexture(diffuseTexName, loadTexture);
            Texture2D lightmapTex = LoadTexture(lightmapTexName, loadTexture);

            var material = new MaterialData
            {
                DiffuseTexture = diffuseTex ?? CreateDefaultTexture(),
                LightmapTexture = lightmapTex,
                Type = MaterialType.LightmappedOpaque,
                BlendState = BlendState.Opaque,
                IsTransparent = false
            };

            return material;
        }

        /// <summary>
        /// Creates a default fallback material.
        /// </summary>
        public MaterialData CreateDefaultMaterial()
        {
            var material = new MaterialData
            {
                DiffuseTexture = CreateDefaultTexture(),
                Type = MaterialType.Opaque,
                DiffuseColor = new Color(255, 0, 255, 255), // Magenta for missing texture
                BlendState = BlendState.Opaque,
                IsTransparent = false
            };

            return material;
        }

        private MaterialData CreateMaterial(string diffuseTexName, string lightmapTexName, Func<string, TPC> loadTexture)
        {
            // Determine material type based on texture and flags
            if (string.IsNullOrEmpty(diffuseTexName))
            {
                return CreateDefaultMaterial();
            }

            // Check if texture has alpha for transparency detection
            TPC tpc = null;
            try
            {
                tpc = loadTexture(diffuseTexName);
            }
            catch
            {
                // Ignore load errors
            }

            bool hasAlpha = tpc != null && (
                tpc.Format() == TPCTextureFormat.RGBA ||
                tpc.Format() == TPCTextureFormat.BGRA ||
                tpc.Format() == TPCTextureFormat.DXT3 ||
                tpc.Format() == TPCTextureFormat.DXT5);

            bool hasLightmap = !string.IsNullOrEmpty(lightmapTexName);

            if (hasLightmap)
            {
                return CreateLightmappedMaterial(diffuseTexName, lightmapTexName, loadTexture);
            }
            else if (hasAlpha)
            {
                // Check alpha test value from TPC
                if (tpc != null && tpc.AlphaTest > 0.01f)
                {
                    return CreateAlphaCutoutMaterial(diffuseTexName, loadTexture, tpc.AlphaTest);
                }
                return CreateAlphaBlendMaterial(diffuseTexName, loadTexture);
            }
            else
            {
                return CreateOpaqueMaterial(diffuseTexName, loadTexture);
            }
        }

        private Texture2D LoadTexture(string textureName, Func<string, TPC> loadTexture)
        {
            if (string.IsNullOrEmpty(textureName))
            {
                return null;
            }

            string key = textureName.ToLowerInvariant();

            // Check cache
            if (_textureCache.TryGetValue(key, out Texture2D cached))
            {
                return cached;
            }

            // Load TPC
            TPC tpc;
            try
            {
                tpc = loadTexture(key);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[KotorMaterialFactory] Error loading texture " + key + ": " + ex.Message);
                return null;
            }

            if (tpc == null)
            {
                return null;
            }

            // Convert to MonoGame texture
            // Note: For materials, textures are always 2D (not cube maps), so we can safely cast to Texture2D
            Texture2D texture;
            try
            {
                Texture convertedTexture = TpcToMonoGameTextureConverter.Convert(tpc, _device);
                // Materials use 2D textures, not cube maps
                if (convertedTexture is TextureCube)
                {
                    Console.WriteLine("[KotorMaterialFactory] Warning: Cube map texture used in material: " + key);
                    return null; // Materials don't support cube maps
                }
                texture = (Texture2D)convertedTexture;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[KotorMaterialFactory] Error converting texture " + key + ": " + ex.Message);
                return null;
            }

            _textureCache[key] = texture;
            return texture;
        }

        /// <summary>
        /// Creates a default magenta texture to indicate missing textures.
        /// </summary>
        private Texture2D CreateDefaultTexture()
        {
            // Create a 1x1 magenta texture
            Texture2D texture = new Texture2D(_device, 1, 1);
            texture.SetData(new[] { new Color(255, 0, 255, 255) });
            return texture;
        }

        /// <summary>
        /// Clears all cached materials and textures.
        /// </summary>
        // Dispose cached textures
        // Based on MonoGame API: https://docs.monogame.net/api/Microsoft.Xna.Framework.Graphics.Texture2D.html
        // Texture2D.Dispose releases graphics resources
        // Source: https://docs.monogame.net/articles/getting_to_know/howto/graphics/HowTo_Load_Texture.html
        public void ClearCache()
        {
            foreach (Texture2D tex in _textureCache.Values)
            {
                tex.Dispose();
            }
            _textureCache.Clear();
            _materialCache.Clear();
        }
    }
}


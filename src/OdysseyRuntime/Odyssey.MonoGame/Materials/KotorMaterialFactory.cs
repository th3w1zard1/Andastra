using System;
using System.Collections.Generic;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Rendering.Materials;
using Stride.Rendering.Materials.ComputeColors;
using Stride.Core.Mathematics;
using CSharpKOTOR.Formats.TPC;
using Odyssey.MonoGame.Converters;
using JetBrains.Annotations;

namespace Odyssey.MonoGame.Materials
{
    /// <summary>
    /// Factory for creating Stride materials from KOTOR texture and material data.
    /// Supports opaque, alpha cutout, alpha blend, additive, and lightmapped materials.
    /// </summary>
    public class KotorMaterialFactory
    {
        private readonly GraphicsDevice _device;
        private readonly Dictionary<string, Material> _materialCache;
        private readonly Dictionary<string, Texture> _textureCache;

        /// <summary>
        /// Creates a new material factory.
        /// </summary>
        /// <param name="device">Graphics device.</param>
        // Initialize material factory with graphics device
        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Graphics.GraphicsDevice.html
        // GraphicsDevice provides access to graphics hardware and resources
        // Material and Texture are Stride rendering types
        // Source: https://doc.stride3d.net/latest/en/manual/graphics/low-level-api/index.html
        public KotorMaterialFactory([NotNull] GraphicsDevice device)
        {
            _device = device ?? throw new ArgumentNullException("device");
            _materialCache = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
            _textureCache = new Dictionary<string, Texture>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Creates or retrieves a material for the given diffuse texture.
        /// </summary>
        /// <param name="diffuseTexName">Diffuse texture name.</param>
        /// <param name="lightmapTexName">Optional lightmap texture name.</param>
        /// <param name="loadTexture">Function to load TPC by name.</param>
        /// <returns>A Stride Material.</returns>
        public Material GetMaterial(
            string diffuseTexName,
            string lightmapTexName,
            Func<string, TPC> loadTexture)
        {
            // Create cache key
            string key = (diffuseTexName ?? "default") + "_" + (lightmapTexName ?? "none");

            // Check cache
            if (_materialCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            // Create new material
            Material material = CreateMaterial(diffuseTexName, lightmapTexName, loadTexture);
            _materialCache[key] = material;

            return material;
        }

        /// <summary>
        /// Creates an opaque material with diffuse texture.
        /// </summary>
        public Material CreateOpaqueMaterial(string diffuseTexName, Func<string, TPC> loadTexture)
        {
            // Create material descriptor
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Rendering.Materials.MaterialDescriptor.html
            // MaterialDescriptor() constructor creates a new material descriptor with default properties
            // MaterialDescriptor defines material properties and features
            // Method signature: MaterialDescriptor()
            // Source: https://doc.stride3d.net/latest/en/manual/graphics/materials/index.html
            var desc = new MaterialDescriptor();

            // Load diffuse texture
            Texture diffuseTex = LoadTexture(diffuseTexName, loadTexture);

            if (diffuseTex != null)
            {
                // Set diffuse map feature
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Rendering.Materials.MaterialDiffuseMapFeature.html
                // MaterialDiffuseMapFeature defines the diffuse color/texture of the material
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Rendering.Materials.ComputeColors.ComputeTextureColor.html
                // ComputeTextureColor samples color from a texture
                // Source: https://doc.stride3d.net/latest/en/manual/graphics/materials/material-features.html
                desc.Attributes.Diffuse = new MaterialDiffuseMapFeature
                {
                    DiffuseMap = new ComputeTextureColor(diffuseTex)
                };
            }
            else
            {
                // Default white diffuse
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Rendering.Materials.ComputeColors.ComputeColor.html
                // ComputeColor(Color4) constructor creates a compute color from a Color4 value
                // Method signature: ComputeColor(Color4 color)
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Color4.html
                // Color4.White is a static property representing white color (R=1, G=1, B=1, A=1)
                // Source: https://doc.stride3d.net/latest/en/manual/graphics/colors.html
                desc.Attributes.Diffuse = new MaterialDiffuseMapFeature
                {
                    DiffuseMap = new ComputeColor(Color4.White)
                };
            }

            // Basic specular
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Rendering.Materials.MaterialGlossinessMapFeature.html
            // MaterialGlossinessMapFeature() constructor creates a new glossiness map feature
            // MaterialGlossinessMapFeature controls surface roughness/glossiness
            // Method signature: MaterialGlossinessMapFeature()
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Rendering.Materials.ComputeColors.ComputeFloat.html
            // ComputeFloat(float) constructor creates a compute float from a constant value
            // Method signature: ComputeFloat(float value)
            // Source: https://doc.stride3d.net/latest/en/manual/graphics/materials/material-features.html
            desc.Attributes.MicroSurface = new MaterialGlossinessMapFeature
            {
                GlossinessMap = new ComputeFloat(0.3f)
            };

            // Create material from descriptor
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Rendering.Material.html
            // Material.New creates a new Material instance from a MaterialDescriptor
            // Method signature: Material.New(GraphicsDevice, MaterialDescriptor)
            // Source: https://doc.stride3d.net/latest/en/manual/graphics/materials/index.html
            return Material.New(_device, desc);
        }

        /// <summary>
        /// Creates an alpha cutout material for transparency with hard edges.
        /// </summary>
        public Material CreateAlphaCutoutMaterial(string diffuseTexName, Func<string, TPC> loadTexture, float alphaThreshold = 0.5f)
        {
            var desc = new MaterialDescriptor();

            Texture diffuseTex = LoadTexture(diffuseTexName, loadTexture);

            if (diffuseTex != null)
            {
                desc.Attributes.Diffuse = new MaterialDiffuseMapFeature
                {
                    DiffuseMap = new ComputeTextureColor(diffuseTex)
                };

                // Enable alpha cutout
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Rendering.Materials.MaterialTransparencyCutoffFeature.html
                // MaterialTransparencyCutoffFeature() constructor creates a new transparency cutoff feature
                // MaterialTransparencyCutoffFeature enables alpha testing (hard transparency edges)
                // Alpha property sets the threshold for alpha testing
                // Method signature: MaterialTransparencyCutoffFeature()
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Rendering.Materials.ComputeColors.ComputeFloat.html
                // ComputeFloat(float) constructor creates a compute float from a constant value
                // Method signature: ComputeFloat(float value)
                // Source: https://doc.stride3d.net/latest/en/manual/graphics/materials/material-features.html
                desc.Attributes.Transparency = new MaterialTransparencyCutoffFeature
                {
                    Alpha = new ComputeFloat(alphaThreshold)
                };
            }

            return Material.New(_device, desc);
        }

        /// <summary>
        /// Creates an alpha blend material for soft transparency.
        /// </summary>
        public Material CreateAlphaBlendMaterial(string diffuseTexName, Func<string, TPC> loadTexture)
        {
            // Create material descriptor
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Rendering.Materials.MaterialDescriptor.html
            // MaterialDescriptor() constructor creates a new material descriptor
            // Method signature: MaterialDescriptor()
            // Source: https://doc.stride3d.net/latest/en/manual/graphics/materials/index.html
            var desc = new MaterialDescriptor();

            Texture diffuseTex = LoadTexture(diffuseTexName, loadTexture);

            if (diffuseTex != null)
            {
                desc.Attributes.Diffuse = new MaterialDiffuseMapFeature
                {
                    DiffuseMap = new ComputeTextureColor(diffuseTex)
                };

                // Enable alpha blending
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Rendering.Materials.MaterialTransparencyBlendFeature.html
                // MaterialTransparencyBlendFeature() constructor creates a new transparency blend feature
                // MaterialTransparencyBlendFeature enables alpha blending (soft transparency)
                // Method signature: MaterialTransparencyBlendFeature()
                // Source: https://doc.stride3d.net/latest/en/manual/graphics/materials/material-features.html
                desc.Attributes.Transparency = new MaterialTransparencyBlendFeature();
            }

            return Material.New(_device, desc);
        }

        /// <summary>
        /// Creates an additive blend material for glowing/emissive effects.
        /// </summary>
        public Material CreateAdditiveMaterial(string diffuseTexName, Func<string, TPC> loadTexture)
        {
            // Create material descriptor
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Rendering.Materials.MaterialDescriptor.html
            // MaterialDescriptor() constructor creates a new material descriptor
            // Method signature: MaterialDescriptor()
            // Source: https://doc.stride3d.net/latest/en/manual/graphics/materials/index.html
            var desc = new MaterialDescriptor();

            Texture diffuseTex = LoadTexture(diffuseTexName, loadTexture);

            if (diffuseTex != null)
            {
                desc.Attributes.Diffuse = new MaterialDiffuseMapFeature
                {
                    DiffuseMap = new ComputeTextureColor(diffuseTex)
                };

                // Make it emissive
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Rendering.Materials.MaterialEmissiveMapFeature.html
                // MaterialEmissiveMapFeature() constructor creates a new emissive map feature
                // MaterialEmissiveMapFeature makes the material emit light
                // EmissiveMap sets the emissive texture, Intensity controls emission strength
                // Method signature: MaterialEmissiveMapFeature()
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Rendering.Materials.ComputeColors.ComputeTextureColor.html
                // ComputeTextureColor(Texture) constructor creates a compute texture color from a texture
                // Method signature: ComputeTextureColor(Texture texture)
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Rendering.Materials.ComputeColors.ComputeFloat.html
                // ComputeFloat(float) constructor creates a compute float from a constant value
                // Method signature: ComputeFloat(float value)
                // Source: https://doc.stride3d.net/latest/en/manual/graphics/materials/material-features.html
                desc.Attributes.Emissive = new MaterialEmissiveMapFeature
                {
                    EmissiveMap = new ComputeTextureColor(diffuseTex),
                    Intensity = new ComputeFloat(2.0f)
                };

                // Additive blending
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Rendering.Materials.MaterialTransparencyAdditiveFeature.html
                // MaterialTransparencyAdditiveFeature() constructor creates a new transparency additive feature
                // MaterialTransparencyAdditiveFeature enables additive blending (glowing effects)
                // Method signature: MaterialTransparencyAdditiveFeature()
                // Source: https://doc.stride3d.net/latest/en/manual/graphics/materials/material-features.html
                desc.Attributes.Transparency = new MaterialTransparencyAdditiveFeature();
            }

            return Material.New(_device, desc);
        }

        /// <summary>
        /// Creates a lightmapped material with baked lighting.
        /// </summary>
        public Material CreateLightmappedMaterial(string diffuseTexName, string lightmapTexName, Func<string, TPC> loadTexture)
        {
            // Create material descriptor
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Rendering.Materials.MaterialDescriptor.html
            // MaterialDescriptor() constructor creates a new material descriptor
            // Method signature: MaterialDescriptor()
            // Source: https://doc.stride3d.net/latest/en/manual/graphics/materials/index.html
            var desc = new MaterialDescriptor();

            Texture diffuseTex = LoadTexture(diffuseTexName, loadTexture);
            Texture lightmapTex = LoadTexture(lightmapTexName, loadTexture);

            if (diffuseTex != null)
            {
                desc.Attributes.Diffuse = new MaterialDiffuseMapFeature
                {
                    DiffuseMap = new ComputeTextureColor(diffuseTex)
                };
            }

            // TODO: Apply lightmap as secondary texture multiply
            // Stride's material system may need custom shader for proper lightmap support
            // For now, we'll use emissive channel as a workaround
            // FIXME: Lightmap should be multiplied with diffuse, not used as emissive
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Rendering.Materials.MaterialEmissiveMapFeature.html
            // MaterialEmissiveMapFeature() constructor creates a new emissive map feature
            // Using emissive as workaround for lightmap - proper implementation would use custom shader
            // Method signature: MaterialEmissiveMapFeature()
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Rendering.Materials.ComputeColors.ComputeTextureColor.html
            // ComputeTextureColor(Texture) constructor creates a compute texture color from a texture
            // Method signature: ComputeTextureColor(Texture texture)
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Rendering.Materials.ComputeColors.ComputeFloat.html
            // ComputeFloat(float) constructor creates a compute float from a constant value
            // Method signature: ComputeFloat(float value)
            if (lightmapTex != null)
            {
                desc.Attributes.Emissive = new MaterialEmissiveMapFeature
                {
                    EmissiveMap = new ComputeTextureColor(lightmapTex),
                    Intensity = new ComputeFloat(0.5f)
                };
            }

            // Create material from descriptor
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Rendering.Material.html
            // Material.New creates a new Material instance from a MaterialDescriptor
            // Method signature: Material.New(GraphicsDevice, MaterialDescriptor)
            // Source: https://doc.stride3d.net/latest/en/manual/graphics/materials/index.html
            return Material.New(_device, desc);
        }

        /// <summary>
        /// Creates a default fallback material.
        /// </summary>
        public Material CreateDefaultMaterial()
        {
            // Create material descriptor
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Rendering.Materials.MaterialDescriptor.html
            // MaterialDescriptor() constructor creates a new material descriptor
            // Method signature: MaterialDescriptor()
            // Source: https://doc.stride3d.net/latest/en/manual/graphics/materials/index.html
            var desc = new MaterialDescriptor();

            // Checkerboard pattern in magenta to indicate missing texture
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Rendering.Materials.MaterialDiffuseMapFeature.html
            // MaterialDiffuseMapFeature() constructor creates a new diffuse map feature
            // Method signature: MaterialDiffuseMapFeature()
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Rendering.Materials.ComputeColors.ComputeColor.html
            // ComputeColor(Color4) constructor creates a compute color from a Color4 value
            // Method signature: ComputeColor(Color4 color)
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Color4.html
            // Color4(float r, float g, float b, float a) constructor creates a color from RGBA float values (0-1)
            // Source: https://doc.stride3d.net/latest/en/manual/graphics/colors.html
            desc.Attributes.Diffuse = new MaterialDiffuseMapFeature
            {
                DiffuseMap = new ComputeColor(new Color4(1f, 0f, 1f, 1f))
            };

            // Create material from descriptor
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Rendering.Material.html
            // Material.New creates a new Material instance from a MaterialDescriptor
            // Method signature: Material.New(GraphicsDevice, MaterialDescriptor)
            // Source: https://doc.stride3d.net/latest/en/manual/graphics/materials/index.html
            return Material.New(_device, desc);
        }

        private Material CreateMaterial(string diffuseTexName, string lightmapTexName, Func<string, TPC> loadTexture)
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

        private Texture LoadTexture(string textureName, Func<string, TPC> loadTexture)
        {
            if (string.IsNullOrEmpty(textureName))
            {
                return null;
            }

            string key = textureName.ToLowerInvariant();

            // Check cache
            if (_textureCache.TryGetValue(key, out var cached))
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

            // Convert to Stride texture
            Texture texture;
            try
            {
                texture = TpcToStrideTextureConverter.Convert(tpc, _device);
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
        /// Clears all cached materials and textures.
        /// </summary>
        // Dispose cached textures
        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Graphics.Texture.html
        // Texture.Dispose releases graphics resources
        // Source: https://doc.stride3d.net/latest/en/manual/graphics/low-level-api/textures-and-render-textures.html
        public void ClearCache()
        {
            foreach (var tex in _textureCache.Values)
            {
                tex.Dispose();
            }
            _textureCache.Clear();
            _materialCache.Clear();
        }
    }
}


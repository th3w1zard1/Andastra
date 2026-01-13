using System;
using System.Numerics;
using Andastra.Runtime.MonoGame.Enums;
using Andastra.Runtime.MonoGame.Interfaces;

namespace Andastra.Game.Graphics.MonoGame.Materials
{
    /// <summary>
    /// Converts KOTOR's legacy Blinn-Phong materials to modern PBR workflow.
    ///
    /// KOTOR Material Model (legacy):
    /// - Diffuse color/texture
    /// - Specular color + power (shininess)
    /// - Self-illumination (emissive)
    /// - Environment map (reflection)
    /// - Lightmap (baked GI)
    /// - Bump map (height-based)
    ///
    /// PBR Material Model (modern):
    /// - Albedo (base color)
    /// - Metallic (0-1)
    /// - Roughness (0-1)
    /// - Normal map
    /// - Ambient occlusion
    /// - Emissive
    /// </summary>
    /// <remarks>
    /// KOTOR Material Converter:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) material/shader system (modern PBR enhancement)
    /// - Located via string references: "glMaterialfv" @ 0x0080ad74 (OpenGL material function), "glColorMaterial" @ 0x0080ad84 (OpenGL color material function)
    /// - "glBindMaterialParameterEXT" @ 0x007b77b0 (OpenGL material parameter binding), "it_materialcloth" @ 0x007cab4c (material cloth item)
    /// - Original implementation: KOTOR uses Blinn-Phong shading model with OpenGL/DirectX fixed-function pipeline
    /// - Material properties: Diffuse, specular (color + power), self-illumination, environment maps, lightmaps
    /// - Shader system: Original engine uses fixed-function pipeline with material parameters (glMaterialfv sets material properties)
    /// - Material loading: Materials loaded from MDL file format, stored in MDL node structures
    /// - This MonoGame implementation: Converts Blinn-Phong materials to modern PBR (Physically Based Rendering)
    /// - Conversion: Specular power → roughness, specular color → metallic estimation, emissive preserved
    /// - Note: Original engine used fixed-function pipeline (glMaterialfv for material properties), PBR is a modern enhancement for better visuals
    /// </remarks>
    public static class KotorMaterialConverter
    {
        /// <summary>
        /// Converts KOTOR material data to a PBR material.
        /// </summary>
        public static PbrMaterial Convert(string name, KotorMaterialData data)
        {
            var material = new PbrMaterial(name, DetermineType(data));

            // Base color
            material.AlbedoColor = data.DiffuseColor;

            // Convert specular power to roughness
            // KOTOR uses Blinn-Phong with power values typically 1-100
            // Roughness = 1 - (power / maxPower)^0.25
            // Higher power = shinier = lower roughness
            float roughness = ConvertSpecularPowerToRoughness(data.SpecularPower);
            material.Roughness = roughness;

            // Estimate metallic from specular color intensity
            // Non-metals have low specular, metals have high colored specular
            float metallic = EstimateMetallicFromSpecular(data.SpecularColor, data.DiffuseColor);
            material.Metallic = metallic;

            // Self-illumination to emissive
            if (data.SelfIllumColor.Length() > 0.01f)
            {
                material.EmissiveColor = data.SelfIllumColor;
                material.EmissiveIntensity = data.SelfIllumColor.Length();
            }

            // Alpha/transparency
            if (data.Alpha < 0.99f)
            {
                material.Opacity = data.Alpha;
                material.Type = MaterialType.AlphaBlend;
            }

            // Handle render hints
            ApplyRenderHints(material, data.RenderHints);

            return material;
        }

        /// <summary>
        /// Converts specular power (shininess) to PBR roughness.
        /// </summary>
        public static float ConvertSpecularPowerToRoughness(float specularPower)
        {
            // Clamp to valid range
            specularPower = Math.Max(1f, Math.Min(specularPower, 256f));

            // Use a perceptually linear conversion
            // Based on: roughness = sqrt(2 / (specularPower + 2))
            // This maps: power 1 -> roughness ~1.0, power 100 -> roughness ~0.14
            float roughness = (float)Math.Sqrt(2.0f / (specularPower + 2.0f));

            return Math.Max(0.04f, Math.Min(roughness, 1.0f));
        }

        /// <summary>
        /// Estimates metallic value from specular characteristics.
        /// </summary>
        public static float EstimateMetallicFromSpecular(Vector3 specularColor, Vector4 diffuseColor)
        {
            // Calculate specular intensity
            float specIntensity = (specularColor.X + specularColor.Y + specularColor.Z) / 3f;

            // Check for colored specular (indicates metal)
            float specColorVariance = Math.Abs(specularColor.X - specularColor.Y) +
                                     Math.Abs(specularColor.Y - specularColor.Z) +
                                     Math.Abs(specularColor.Z - specularColor.X);

            // Compare specular color to diffuse color (metals have similar spec/diff)
            Vector3 diffuseRgb = new Vector3(diffuseColor.X, diffuseColor.Y, diffuseColor.Z);
            float colorSimilarity = Vector3.Dot(
                Vector3.Normalize(specularColor + new Vector3(0.001f)),
                Vector3.Normalize(diffuseRgb + new Vector3(0.001f)));

            // High intensity + colored specular + similar to diffuse = metallic
            float metallic = 0f;

            if (specIntensity > 0.5f)
            {
                metallic = specIntensity * 0.5f;

                // Boost if specular is colored
                if (specColorVariance > 0.1f)
                {
                    metallic += 0.2f;
                }

                // Boost if specular matches diffuse color (characteristic of metals)
                if (colorSimilarity > 0.9f && specIntensity > 0.6f)
                {
                    metallic += 0.3f;
                }
            }

            return Math.Max(0f, Math.Min(metallic, 1f));
        }

        /// <summary>
        /// Converts a height map to a normal map.
        /// </summary>
        public static void ConvertHeightToNormal(byte[] heightData, int width, int height, byte[] normalOutput, float strength = 1.0f)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Sample neighboring heights
                    float h_l = GetHeight(heightData, width, height, x - 1, y);
                    float h_r = GetHeight(heightData, width, height, x + 1, y);
                    float h_u = GetHeight(heightData, width, height, x, y - 1);
                    float h_d = GetHeight(heightData, width, height, x, y + 1);

                    // Calculate normal from height differences
                    float dx = (h_l - h_r) * strength;
                    float dy = (h_u - h_d) * strength;

                    Vector3 normal = Vector3.Normalize(new Vector3(dx, dy, 1.0f));

                    // Convert to RGB [0, 255]
                    int idx = (y * width + x) * 4;
                    normalOutput[idx + 0] = (byte)((normal.X * 0.5f + 0.5f) * 255);
                    normalOutput[idx + 1] = (byte)((normal.Y * 0.5f + 0.5f) * 255);
                    normalOutput[idx + 2] = (byte)((normal.Z * 0.5f + 0.5f) * 255);
                    normalOutput[idx + 3] = 255;
                }
            }
        }

        private static float GetHeight(byte[] data, int width, int height, int x, int y)
        {
            x = Math.Max(0, Math.Min(x, width - 1));
            y = Math.Max(0, Math.Min(y, height - 1));
            return data[y * width + x] / 255f;
        }

        private static MaterialType DetermineType(KotorMaterialData data)
        {
            if ((data.RenderHints & KotorRenderHints.Additive) != 0)
                return MaterialType.Additive;
            if ((data.RenderHints & KotorRenderHints.Transparent) != 0)
                return MaterialType.AlphaBlend;
            if ((data.RenderHints & KotorRenderHints.Hologram) != 0)
                return MaterialType.Hologram;
            if ((data.RenderHints & KotorRenderHints.Saber) != 0)
                return MaterialType.Emissive;
            if (!string.IsNullOrEmpty(data.LightmapMap))
                return MaterialType.LightmappedOpaque;
            if (data.Alpha < 0.99f)
                return MaterialType.AlphaBlend;

            return MaterialType.Opaque;
        }

        private static void ApplyRenderHints(PbrMaterial material, KotorRenderHints hints)
        {
            if ((hints & KotorRenderHints.TwoSided) != 0)
            {
                material.DoubleSided = true;
            }

            if ((hints & KotorRenderHints.NoShadow) != 0)
            {
                material.CastShadows = false;
            }

            if ((hints & KotorRenderHints.Hologram) != 0)
            {
                // Hologram effect: blue tint, emissive, scanlines
                material.EmissiveColor = new Vector3(0.3f, 0.5f, 1.0f);
                material.EmissiveIntensity = 2.0f;
                material.Opacity = 0.7f;
                material.AlbedoColor = new Vector4(0.3f, 0.5f, 1.0f, 0.7f);
            }

            if ((hints & KotorRenderHints.Saber) != 0)
            {
                // Lightsaber blade: highly emissive, additive
                material.Type = MaterialType.Additive;
                material.EmissiveIntensity = 10.0f;
                material.CastShadows = false;
            }

            if ((hints & KotorRenderHints.Skin) != 0)
            {
                // Character skin: subsurface scattering
                material.Type = MaterialType.Subsurface;
                material.SubsurfaceColor = new Vector3(1.0f, 0.2f, 0.1f);
                material.SubsurfaceRadius = 0.5f;
            }
        }
    }
}


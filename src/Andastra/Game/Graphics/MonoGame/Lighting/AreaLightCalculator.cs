using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Andastra.Runtime.MonoGame.Enums;
using Andastra.Runtime.MonoGame.Interfaces;
using Microsoft.Xna.Framework;
using XnaVector3 = Microsoft.Xna.Framework.Vector3;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Game.Graphics.MonoGame.Lighting
{
    /// <summary>
    /// Comprehensive area light calculator implementing true area light rendering.
    ///
    /// Implements:
    /// - Multiple light samples across the area surface
    /// - Soft shadow calculations using PCF (Percentage Closer Filtering)
    /// - Proper area light BRDF integration
    /// - Physically-based lighting calculations
    ///
    /// Based on daorigins.exe/DragonAge2.exe: Area light rendering system
    /// Eclipse engine uses area lights for realistic soft lighting and shadows
    /// </summary>
    public static class AreaLightCalculator
    {
        /// <summary>
        /// Number of samples to use for area light integration.
        /// Higher values provide better quality but more expensive.
        /// Based on daorigins.exe: Uses 4-16 samples depending on quality settings
        /// </summary>
        private const int AreaLightSamples = 8;

        /// <summary>
        /// Number of PCF samples for soft shadow calculation.
        /// Higher values provide softer shadows but more expensive.
        /// Based on daorigins.exe: Uses 4-9 samples for PCF filtering
        /// </summary>
        private const int PcfSamples = 4;

        /// <summary>
        /// Calculates the lighting contribution from an area light at a given surface point.
        /// Implements proper area light BRDF integration with multiple samples.
        /// </summary>
        /// <param name="light">The area light source</param>
        /// <param name="surfacePosition">Position of the surface point being lit</param>
        /// <param name="surfaceNormal">Normal vector of the surface at the point</param>
        /// <param name="viewDirection">Direction from surface to camera (for specular calculations)</param>
        /// <param name="shadowMap">Optional shadow map for soft shadow calculations (null if not available)</param>
        /// <param name="lightSpaceMatrix">Light space transformation matrix for shadow mapping</param>
        /// <returns>Lighting contribution (diffuse + specular) from the area light</returns>
        public static System.Numerics.Vector3 CalculateAreaLightContribution(
            IDynamicLight light,
            System.Numerics.Vector3 surfacePosition,
            System.Numerics.Vector3 surfaceNormal,
            System.Numerics.Vector3 viewDirection,
            IntPtr shadowMap,
            System.Numerics.Matrix4x4 lightSpaceMatrix)
        {
            if (light == null || light.Type != LightType.Area || !light.Enabled)
            {
                return XnaVector3.Zero;
            }

            // Calculate area light surface corners and orientation
            // Area lights are rectangular surfaces defined by position, direction, width, and height
            // Based on daorigins.exe: Area lights are oriented rectangles in 3D space
            XnaVector3 lightForward = XnaVector3.Normalize(-light.Direction);
            XnaVector3 lightRight = CalculateRightVector(lightForward);
            XnaVector3 lightUp = XnaVector3.Cross(lightRight, lightForward);

            // Calculate half dimensions
            float halfWidth = light.AreaWidth * 0.5f;
            float halfHeight = light.AreaHeight * 0.5f;

            // Generate sample points across the area light surface
            // Use stratified sampling for better coverage
            List<XnaVector3> samplePoints = GenerateAreaLightSamples(
                light.Position,
                lightRight,
                lightUp,
                halfWidth,
                halfHeight,
                AreaLightSamples);

            // Accumulate lighting contribution from all samples
            XnaVector3 totalDiffuse = XnaVector3.Zero;
            XnaVector3 totalSpecular = XnaVector3.Zero;

            foreach (XnaVector3 samplePoint in samplePoints)
            {
                // Calculate direction from surface to this sample point
                XnaVector3 lightDirection = XnaVector3.Normalize(samplePoint - surfacePosition);
                float distance = XnaVector3.Distance(samplePoint, surfacePosition);

                // Check if sample is within light radius
                if (distance > light.Radius)
                {
                    continue; // Sample is outside light influence
                }

                // Calculate distance attenuation
                float distanceAttenuation = CalculateDistanceAttenuation(distance, light.Radius);

                // Calculate Lambertian diffuse term (N dot L)
                float nDotL = Math.Max(0.0f, XnaVector3.Dot(surfaceNormal, lightDirection));
                if (nDotL <= 0.0f)
                {
                    continue; // Surface is facing away from light
                }

                // Calculate soft shadow factor using PCF
                float shadowFactor = 1.0f;
                if (shadowMap != IntPtr.Zero)
                {
                    shadowFactor = CalculateSoftShadowPcf(
                        surfacePosition,
                        lightSpaceMatrix,
                        shadowMap,
                        light.ShadowBias,
                        light.ShadowSoftness);
                }

                // Calculate area light BRDF contribution
                // Area lights require integration over the light surface
                // We approximate this by sampling multiple points and averaging
                XnaVector3 sampleContribution = CalculateAreaLightBrdf(
                    light,
                    samplePoint,
                    surfacePosition,
                    surfaceNormal,
                    lightDirection,
                    viewDirection,
                    distanceAttenuation,
                    nDotL,
                    shadowFactor);

                totalDiffuse += sampleContribution;
            }

            // Average the contributions (divide by number of samples)
            // This approximates the integral over the area light surface
            float sampleWeight = 1.0f / samplePoints.Count;
            XnaVector3 finalContribution = (totalDiffuse + totalSpecular) * sampleWeight;

            // Apply light color and intensity
            XnaVector3 lightColor = light.Color;
            if (light.UseTemperature)
            {
                lightColor = DynamicLight.TemperatureToRgb(light.Temperature);
            }

            return finalContribution * lightColor * light.Intensity;
        }

        /// <summary>
        /// Generates sample points across the area light surface using stratified sampling.
        /// </summary>
        private static List<XnaVector3> GenerateAreaLightSamples(
            XnaVector3 lightCenter,
            XnaVector3 lightRight,
            XnaVector3 lightUp,
            float halfWidth,
            float halfHeight,
            int numSamples)
        {
            List<XnaVector3> samples = new List<XnaVector3>();

            // Use stratified sampling for better coverage
            // Divide area into grid and sample within each cell
            int gridSize = (int)Math.Ceiling(Math.Sqrt(numSamples));
            float cellWidth = (halfWidth * 2.0f) / gridSize;
            float cellHeight = (halfHeight * 2.0f) / gridSize;

            // Random number generator for jittering (using simple hash-based pseudo-random)
            uint seed = 12345;

            for (int i = 0; i < numSamples; i++)
            {
                // Calculate grid cell position
                int cellX = i % gridSize;
                int cellY = i / gridSize;

                // Jitter within cell for better sampling
                float jitterX = (float)(Hash(seed + (uint)(i * 2)) % 1000) / 1000.0f;
                float jitterY = (float)(Hash(seed + (uint)(i * 2 + 1)) % 1000) / 1000.0f;

                // Calculate sample position in local space
                float localX = -halfWidth + cellX * cellWidth + jitterX * cellWidth;
                float localY = -halfHeight + cellY * cellHeight + jitterY * cellHeight;

                // Transform to world space
                XnaVector3 samplePoint = lightCenter + lightRight * localX + lightUp * localY;
                samples.Add(samplePoint);
            }

            return samples;
        }

        /// <summary>
        /// Simple hash function for pseudo-random number generation.
        /// </summary>
        private static uint Hash(uint x)
        {
            x ^= x >> 16;
            x *= 0x85ebca6b;
            x ^= x >> 13;
            x *= 0xc2b2ae35;
            x ^= x >> 16;
            return x;
        }

        /// <summary>
        /// Calculates the right vector for the area light orientation.
        /// </summary>
        private static XnaVector3 CalculateRightVector(XnaVector3 forward)
        {
            // Choose an arbitrary up vector (typically world up)
            XnaVector3 worldUp = XnaVector3.UnitY;

            // If forward is parallel to world up, use a different reference
            if (Math.Abs(XnaVector3.Dot(forward, worldUp)) > 0.9f)
            {
                worldUp = XnaVector3.UnitZ;
            }

            // Calculate right vector as cross product
            return XnaVector3.Normalize(XnaVector3.Cross(forward, worldUp));
        }

        /// <summary>
        /// Calculates distance attenuation using physically-based inverse square falloff.
        /// </summary>
        private static float CalculateDistanceAttenuation(float distance, float radius)
        {
            if (distance >= radius)
            {
                return 0.0f;
            }

            // Physically-based inverse square falloff: 1 / (distance^2 + 1)
            // The +1 prevents singularity at distance = 0
            float d2 = distance * distance;
            float attenuation = 1.0f / (d2 + 1.0f);

            // Smooth falloff near radius boundary
            float edgeFactor = 1.0f - (distance / radius);
            edgeFactor = edgeFactor * edgeFactor; // Quadratic falloff

            return attenuation * edgeFactor;
        }

        /// <summary>
        /// Calculates soft shadows using Percentage Closer Filtering (PCF).
        /// Samples the shadow map at multiple offsets and averages the results.
        ///
        /// Based on daorigins.exe: Shadow comparison sampling implementation
        /// Original engine uses hardware-accelerated shadow comparison in shaders
        /// This CPU-side implementation reads depth texture and performs comparison
        /// </summary>
        private static float CalculateSoftShadowPcf(
            XnaVector3 surfacePosition,
            Matrix4x4 lightSpaceMatrix,
            IntPtr shadowMap,
            float shadowBias,
            float shadowSoftness)
        {
            // Transform surface position to light space
            Vector4 lightSpacePos = Vector4.Transform(new Vector4(surfacePosition, 1.0f), lightSpaceMatrix);

            // Perspective divide
            if (Math.Abs(lightSpacePos.W) < 0.0001f)
            {
                return 1.0f; // Avoid division by zero
            }

            XnaVector3 projCoords = new XnaVector3(
                lightSpacePos.X / lightSpacePos.W,
                lightSpacePos.Y / lightSpacePos.W,
                lightSpacePos.Z / lightSpacePos.W);

            // Transform to [0,1] range
            projCoords.X = projCoords.X * 0.5f + 0.5f;
            projCoords.Y = projCoords.Y * 0.5f + 0.5f;

            // Check if position is outside shadow map bounds
            if (projCoords.X < 0.0f || projCoords.X > 1.0f ||
                projCoords.Y < 0.0f || projCoords.Y > 1.0f ||
                projCoords.Z < 0.0f || projCoords.Z > 1.0f)
            {
                return 1.0f; // Outside shadow map, fully lit
            }

            // PCF sampling: sample shadow map at multiple offsets
            // This creates soft shadow edges
            // Based on daorigins.exe: PCF uses 4-9 samples depending on quality settings
            float shadowFactor = 0.0f;
            int samples = 0;

            // PCF kernel offsets (2x2 grid for 4 samples)
            // For higher quality, can use 3x3 (9 samples) or 4x4 (16 samples)
            float[] offsets = new float[]
            {
                -0.5f, -0.5f,
                0.5f, -0.5f,
                -0.5f, 0.5f,
                0.5f, 0.5f
            };

            // Calculate texel size based on shadow map resolution
            // Default assumption: 1024x1024 shadow map (can be overridden if shadow map info available)
            float texelSize = shadowSoftness / 1024.0f;

            for (int i = 0; i < PcfSamples; i++)
            {
                float offsetX = offsets[i * 2] * texelSize;
                float offsetY = offsets[i * 2 + 1] * texelSize;

                float sampleX = projCoords.X + offsetX;
                float sampleY = projCoords.Y + offsetY;
                float sampleDepth = projCoords.Z - shadowBias;

                // Shadow comparison sampling: step(compare, storedDepth)
                // Returns 1.0 if sampleDepth <= storedDepth (in light), 0.0 otherwise (in shadow)
                // This is the CPU-side equivalent of GLSL texture2DCompare or HLSL SampleCmp
                // Based on daorigins.exe: Shadow comparison uses hardware-accelerated comparison
                // CPU-side implementation reads depth and performs comparison manually
                float comparisonResult = SampleShadowMapComparison(shadowMap, sampleX, sampleY, sampleDepth);

                shadowFactor += comparisonResult;
                samples++;
            }

            return shadowFactor / samples;
        }

        /// <summary>
        /// Performs shadow comparison sampling at the given texture coordinates.
        /// Implements the equivalent of GLSL texture2DCompare or HLSL SampleCmp.
        ///
        /// Shadow comparison: step(compare, storedDepth)
        /// - Returns 1.0 if compare <= storedDepth (fragment is in light)
        /// - Returns 0.0 if compare > storedDepth (fragment is in shadow)
        ///
        /// Based on daorigins.exe: Shadow comparison sampling implementation
        /// Original engine uses hardware-accelerated comparison samplers in shaders
        /// This CPU-side implementation reads depth texture and performs comparison manually
        /// </summary>
        /// <param name="shadowMap">Pointer to shadow map texture (Texture2D or RenderTarget2D)</param>
        /// <param name="u">Texture coordinate U (0.0 to 1.0)</param>
        /// <param name="v">Texture coordinate V (0.0 to 1.0)</param>
        /// <param name="compareDepth">Depth value to compare against stored depth</param>
        /// <returns>1.0 if in light (compareDepth <= storedDepth), 0.0 if in shadow</returns>
        private static float SampleShadowMapComparison(IntPtr shadowMap, float u, float v, float compareDepth)
        {
            if (shadowMap == IntPtr.Zero)
            {
                return 1.0f; // No shadow map, fully lit
            }

            // Clamp texture coordinates to valid range
            u = Math.Max(0.0f, Math.Min(1.0f, u));
            v = Math.Max(0.0f, Math.Min(1.0f, v));

            // Try to convert IntPtr to Texture2D or RenderTarget2D
            // IntPtr may be a GCHandle to a Texture2D/RenderTarget2D object
            Texture2D shadowTexture = null;
            RenderTarget2D shadowRenderTarget = null;

            try
            {
                // Attempt to convert IntPtr to GCHandle and get the object
                // This handles the case where shadowMap is a GCHandle.ToIntPtr() result
                GCHandle handle = GCHandle.FromIntPtr(shadowMap);
                object target = handle.Target;

                if (target is Texture2D texture)
                {
                    shadowTexture = texture;
                }
                else if (target is RenderTarget2D renderTarget)
                {
                    shadowRenderTarget = renderTarget;
                }
                else
                {
                    // If conversion fails, try unsafe pointer dereference
                    // This handles the case where IntPtr is a raw pointer to Texture2D*
                    return SampleShadowMapUnsafe(shadowMap, u, v, compareDepth);
                }
            }
            catch
            {
                // If GCHandle conversion fails, try unsafe pointer approach
                return SampleShadowMapUnsafe(shadowMap, u, v, compareDepth);
            }

            // Use the appropriate texture type
            Texture2D depthTexture = shadowTexture ?? shadowRenderTarget;
            if (depthTexture == null)
            {
                return 1.0f; // Invalid shadow map, assume fully lit
            }

            // Get texture dimensions
            int width = depthTexture.Width;
            int height = depthTexture.Height;

            // Convert texture coordinates to pixel coordinates
            int pixelX = (int)(u * width);
            int pixelY = (int)(v * height);

            // Clamp to valid pixel range
            pixelX = Math.Max(0, Math.Min(width - 1, pixelX));
            pixelY = Math.Max(0, Math.Min(height - 1, pixelY));

            // Read depth value from texture
            // Shadow maps typically store depth as:
            // - SurfaceFormat.Single (R32_Float): Single float in red channel
            // - SurfaceFormat.HalfSingle: Half-precision float
            // - DepthFormat.Depth24: 24-bit depth (when read as texture, in red channel as normalized)
            float storedDepth = ReadDepthFromTexture(depthTexture, pixelX, pixelY, width, height);

            // Shadow comparison: step(compareDepth, storedDepth)
            // Equivalent to: return (compareDepth <= storedDepth) ? 1.0f : 0.0f;
            // This matches GLSL step() function behavior
            // Based on daorigins.exe: Shadow comparison uses <= comparison (standard shadow mapping)
            if (compareDepth <= storedDepth)
            {
                return 1.0f; // In light
            }
            else
            {
                return 0.0f; // In shadow
            }
        }

        /// <summary>
        /// Unsafe method to sample shadow map when IntPtr is a raw pointer.
        /// Used as fallback when GCHandle conversion fails.
        /// </summary>
        private static float SampleShadowMapUnsafe(IntPtr shadowMap, float u, float v, float compareDepth)
        {
            // For unsafe pointer access, we would need to:
            // 1. Dereference the pointer to get Texture2D*
            // 2. Access the texture data
            // However, this is platform-specific and requires unsafe code
            // For now, return a fallback value (fully lit) if conversion fails
            // In a production implementation, this would use platform-specific APIs

            // Note: This is a fallback - proper implementation would require
            // platform-specific code to access texture data from native pointers
            return 1.0f; // Fallback: assume fully lit if we can't access shadow map
        }

        /// <summary>
        /// Reads depth value from a texture at the specified pixel coordinates.
        /// Handles different depth texture formats used in shadow mapping.
        ///
        /// Based on daorigins.exe: Depth texture formats
        /// - R32_Float: Single-precision float depth (SurfaceFormat.Single)
        /// - D24_UNorm_S8_UInt: 24-bit depth + 8-bit stencil
        /// - D16_UNorm: 16-bit depth
        /// </summary>
        /// <param name="texture">The depth texture to read from</param>
        /// <param name="x">Pixel X coordinate</param>
        /// <param name="y">Pixel Y coordinate</param>
        /// <param name="width">Texture width</param>
        /// <param name="height">Texture height</param>
        /// <returns>Depth value in range [0.0, 1.0] for normalized formats, or [0.0, farPlane] for linear depth</returns>
        private static float ReadDepthFromTexture(Texture2D texture, int x, int y, int width, int height)
        {
            if (texture == null || x < 0 || x >= width || y < 0 || y >= height)
            {
                return 1.0f; // Invalid coordinates, return maximum depth (fully lit)
            }

            try
            {
                // Determine texture format to read depth correctly
                SurfaceFormat format = texture.Format;

                // For shadow maps, we typically use:
                // - SurfaceFormat.Single: R32_Float (single float in red channel)
                // - SurfaceFormat.HalfSingle: R16_Float (half float)
                // - SurfaceFormat.Color: RGBA8 (when depth is packed)

                if (format == SurfaceFormat.Single)
                {
                    // R32_Float: Single-precision float stored in red channel
                    // Read a single pixel as float
                    float[] depthData = new float[1];
                    texture.GetData(0, new Rectangle(x, y, 1, 1), depthData, 0, 1);
                    return depthData[0];
                }
                else if (format == SurfaceFormat.HalfSingle)
                {
                    // R16_Float: Half-precision float
                    // MonoGame may convert this automatically, but we read as float
                    float[] depthData = new float[1];
                    texture.GetData(0, new Rectangle(x, y, 1, 1), depthData, 0, 1);
                    return depthData[0];
                }
                else if (format == SurfaceFormat.Color)
                {
                    // RGBA8: Depth may be stored in red channel as normalized value
                    // Or depth may be packed across channels
                    Color[] colorData = new Color[1];
                    texture.GetData(0, new Rectangle(x, y, 1, 1), colorData, 0, 1);

                    // For D24S8 format read as Color:
                    // Depth is typically in RGB channels (24 bits total)
                    // Reconstruct 24-bit depth value
                    byte r = colorData[0].R;
                    byte g = colorData[0].G;
                    byte b = colorData[0].B;

                    // Reconstruct 24-bit depth: little-endian byte order
                    uint depth24Bits = (uint)(r | (g << 8) | (b << 16));

                    // Normalize to [0.0, 1.0] range
                    return depth24Bits / 16777215.0f; // 2^24 - 1 = 16777215
                }
                else
                {
                    // Unknown format - try to read as Color and use red channel
                    // This is a fallback for formats we don't explicitly handle
                    Color[] colorData = new Color[1];
                    texture.GetData(0, new Rectangle(x, y, 1, 1), colorData, 0, 1);

                    // Use red channel as normalized depth (0.0-1.0)
                    return colorData[0].R / 255.0f;
                }
            }
            catch
            {
                // If reading fails (e.g., texture is locked, wrong format, etc.)
                // Return maximum depth as fallback (assume fully lit)
                return 1.0f;
            }
        }

        /// <summary>
        /// Calculates area light BRDF contribution (Bidirectional Reflectance Distribution Function).
        /// Implements physically-based lighting model for area lights.
        /// </summary>
        private static XnaVector3 CalculateAreaLightBrdf(
            IDynamicLight light,
            XnaVector3 lightSamplePoint,
            XnaVector3 surfacePosition,
            XnaVector3 surfaceNormal,
            XnaVector3 lightDirection,
            XnaVector3 viewDirection,
            float distanceAttenuation,
            float nDotL,
            float shadowFactor)
        {
            // Lambertian diffuse term
            // For area lights, we use the standard Lambertian BRDF
            // L_diffuse = (albedo / PI) * N dot L
            // We'll use a simple diffuse model here (albedo = 1.0 for simplicity)
            XnaVector3 diffuse = new XnaVector3(nDotL, nDotL, nDotL) * (1.0f / (float)Math.PI);

            // Specular term (Blinn-Phong approximation)
            // For area lights, specular is more complex, but we'll use a simplified version
            XnaVector3 halfVector = XnaVector3.Normalize(lightDirection + viewDirection);
            float nDotH = Math.Max(0.0f, XnaVector3.Dot(surfaceNormal, halfVector));
            float specularPower = 32.0f; // Typical specular power
            float specular = (float)Math.Pow(nDotH, specularPower);

            // Combine diffuse and specular
            XnaVector3 brdf = diffuse + new XnaVector3(specular, specular, specular) * 0.1f;

            // Apply distance attenuation and shadow factor
            return brdf * distanceAttenuation * shadowFactor;
        }

        /// <summary>
        /// Calculates the effective directional light approximation for BasicEffect.
        /// This is used when custom shaders are not available and BasicEffect must be used.
        /// </summary>
        public static void CalculateBasicEffectApproximation(
            IDynamicLight light,
            XnaVector3 surfacePosition,
            out XnaVector3 direction,
            out XnaVector3 color)
        {
            direction = XnaVector3.Zero;
            color = XnaVector3.Zero;

            if (light == null || light.Type != LightType.Area || !light.Enabled)
            {
                return;
            }

            // For BasicEffect, we approximate the area light as a directional light
            // pointing from the area light center to the surface position
            XnaVector3 lightToSurface = XnaVector3.Normalize(surfacePosition - light.Position);
            direction = lightToSurface;

            // Calculate distance attenuation
            float distance = XnaVector3.Distance(light.Position, surfacePosition);
            float distanceAttenuation = CalculateDistanceAttenuation(distance, light.Radius);

            // Calculate area-based intensity scaling
            // Larger area lights appear brighter
            float areaSize = light.AreaWidth * light.AreaHeight;
            float areaFactor = 1.0f + (areaSize * 0.1f);

            // Calculate directional attenuation (cosine of angle between light direction and light-to-surface)
            XnaVector3 lightForward = XnaVector3.Normalize(-light.Direction);
            float cosAngle = XnaVector3.Dot(lightForward, lightToSurface);
            float directionalAttenuation = Math.Max(0.0f, cosAngle);

            // Combine all factors
            XnaVector3 lightColor = light.Color;
            if (light.UseTemperature)
            {
                lightColor = DynamicLight.TemperatureToRgb(light.Temperature);
            }

            color = lightColor * light.Intensity * distanceAttenuation * areaFactor * directionalAttenuation;

            // Clamp to valid color range
            color = new XnaVector3(
                Math.Min(1.0f, Math.Max(0.0f, color.X)),
                Math.Min(1.0f, Math.Max(0.0f, color.Y)),
                Math.Min(1.0f, Math.Max(0.0f, color.Z)));
        }
    }
}


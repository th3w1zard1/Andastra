using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using Odyssey.MonoGame.Enums;
using Odyssey.MonoGame.Interfaces;
using Odyssey.MonoGame.Materials;

namespace Odyssey.MonoGame.Remix
{
    /// <summary>
    /// Exports materials in a format compatible with RTX Remix.
    /// 
    /// Remix uses a JSON-based material definition format that allows
    /// users to override game materials with PBR versions. This exporter
    /// generates both the material definitions and replacement textures.
    /// </summary>
    public class RemixMaterialExporter
    {
        private readonly string _outputPath;
        private readonly Dictionary<string, RemixMaterialDef> _materials;
        
        public RemixMaterialExporter(string outputPath)
        {
            _outputPath = outputPath;
            _materials = new Dictionary<string, RemixMaterialDef>();
            
            Directory.CreateDirectory(outputPath);
            Directory.CreateDirectory(Path.Combine(outputPath, "textures"));
            Directory.CreateDirectory(Path.Combine(outputPath, "meshes"));
        }
        
        /// <summary>
        /// Exports a PBR material for Remix.
        /// </summary>
        public void ExportMaterial(PbrMaterial material, string originalTexturePath)
        {
            var def = new RemixMaterialDef
            {
                Hash = GenerateMaterialHash(originalTexturePath),
                AlbedoTexture = material.AlbedoTexture != IntPtr.Zero 
                    ? ExportTexturePath(material.Name + "_albedo") : null,
                NormalTexture = material.NormalTexture != IntPtr.Zero 
                    ? ExportTexturePath(material.Name + "_normal") : null,
                RoughnessTexture = material.RoughnessTexture != IntPtr.Zero 
                    ? ExportTexturePath(material.Name + "_roughness") : null,
                MetallicTexture = material.MetallicTexture != IntPtr.Zero 
                    ? ExportTexturePath(material.Name + "_metallic") : null,
                EmissiveTexture = material.EmissiveTexture != IntPtr.Zero 
                    ? ExportTexturePath(material.Name + "_emissive") : null,
                AlbedoColor = new float[] { 
                    material.AlbedoColor.X, 
                    material.AlbedoColor.Y, 
                    material.AlbedoColor.Z 
                },
                Roughness = material.Roughness,
                Metallic = material.Metallic,
                EmissiveIntensity = material.EmissiveIntensity,
                IsTransparent = material.Type == MaterialType.AlphaBlend || 
                               material.Type == MaterialType.AlphaCutout,
                AlphaCutoff = material.AlphaCutoff,
                ThinFilm = false,
                Subsurface = material.Type == MaterialType.Subsurface,
                SubsurfaceRadius = material.SubsurfaceRadius,
                SubsurfaceColor = new float[] {
                    material.SubsurfaceColor.X,
                    material.SubsurfaceColor.Y,
                    material.SubsurfaceColor.Z
                }
            };
            
            _materials[material.Name] = def;
        }
        
        /// <summary>
        /// Exports a KOTOR material for Remix.
        /// </summary>
        public void ExportKotorMaterial(string name, KotorMaterialData data)
        {
            // Convert to PBR first
            var pbrMaterial = KotorMaterialConverter.Convert(name, data);
            ExportMaterial(pbrMaterial, data.DiffuseMap);
        }
        
        /// <summary>
        /// Exports a light for Remix.
        /// </summary>
        public RemixLightDef ExportLight(IDynamicLight light)
        {
            return new RemixLightDef
            {
                Type = light.Type == LightType.Directional ? "distant" :
                       light.Type == LightType.Spot ? "spot" : "sphere",
                Position = new float[] { light.Position.X, light.Position.Y, light.Position.Z },
                Direction = new float[] { light.Direction.X, light.Direction.Y, light.Direction.Z },
                Color = new float[] { light.Color.X, light.Color.Y, light.Color.Z },
                Intensity = light.Intensity,
                Radius = light.Type == LightType.Point ? light.Radius * 0.01f : 0,
                ConeAngle = light.Type == LightType.Spot ? light.OuterConeAngle : 0,
                ConeSoftness = light.Type == LightType.Spot ? 
                    (light.OuterConeAngle - light.InnerConeAngle) / light.OuterConeAngle : 0
            };
        }
        
        /// <summary>
        /// Writes all material definitions to the output path.
        /// </summary>
        public void WriteManifest()
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"materials\": [");
            
            int count = 0;
            foreach (var kvp in _materials)
            {
                var mat = kvp.Value;
                sb.AppendLine("    {");
                sb.AppendLine("      \"hash\": \"" + mat.Hash + "\",");
                
                if (!string.IsNullOrEmpty(mat.AlbedoTexture))
                    sb.AppendLine("      \"albedoTexture\": \"" + mat.AlbedoTexture + "\",");
                if (!string.IsNullOrEmpty(mat.NormalTexture))
                    sb.AppendLine("      \"normalTexture\": \"" + mat.NormalTexture + "\",");
                if (!string.IsNullOrEmpty(mat.RoughnessTexture))
                    sb.AppendLine("      \"roughnessTexture\": \"" + mat.RoughnessTexture + "\",");
                if (!string.IsNullOrEmpty(mat.MetallicTexture))
                    sb.AppendLine("      \"metallicTexture\": \"" + mat.MetallicTexture + "\",");
                if (!string.IsNullOrEmpty(mat.EmissiveTexture))
                    sb.AppendLine("      \"emissiveTexture\": \"" + mat.EmissiveTexture + "\",");
                
                sb.AppendLine("      \"albedoColor\": [" + 
                    mat.AlbedoColor[0].ToString("F3") + ", " + 
                    mat.AlbedoColor[1].ToString("F3") + ", " + 
                    mat.AlbedoColor[2].ToString("F3") + "],");
                sb.AppendLine("      \"roughness\": " + mat.Roughness.ToString("F3") + ",");
                sb.AppendLine("      \"metallic\": " + mat.Metallic.ToString("F3") + ",");
                sb.AppendLine("      \"emissiveIntensity\": " + mat.EmissiveIntensity.ToString("F3") + ",");
                sb.AppendLine("      \"transparent\": " + (mat.IsTransparent ? "true" : "false") + ",");
                sb.AppendLine("      \"alphaCutoff\": " + mat.AlphaCutoff.ToString("F3") + ",");
                sb.AppendLine("      \"subsurface\": " + (mat.Subsurface ? "true" : "false"));
                
                sb.Append("    }");
                if (++count < _materials.Count)
                    sb.AppendLine(",");
                else
                    sb.AppendLine();
            }
            
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            
            File.WriteAllText(Path.Combine(_outputPath, "materials.json"), sb.ToString());
        }
        
        /// <summary>
        /// Generates a Remix-compatible material hash from texture path.
        /// </summary>
        private string GenerateMaterialHash(string texturePath)
        {
            if (string.IsNullOrEmpty(texturePath))
            {
                return Guid.NewGuid().ToString("N").Substring(0, 16);
            }
            
            // Remix uses XXHash64 for material identification
            // For now, use a simple hash
            uint hash = 0;
            foreach (char c in texturePath.ToLowerInvariant())
            {
                hash = hash * 31 + c;
            }
            
            return hash.ToString("X8");
        }
        
        private string ExportTexturePath(string name)
        {
            return "textures/" + name + ".dds";
        }
    }
    
    /// <summary>
    /// Remix material definition.
    /// </summary>
    public class RemixMaterialDef
    {
        public string Hash;
        public string AlbedoTexture;
        public string NormalTexture;
        public string RoughnessTexture;
        public string MetallicTexture;
        public string EmissiveTexture;
        public float[] AlbedoColor;
        public float Roughness;
        public float Metallic;
        public float EmissiveIntensity;
        public bool IsTransparent;
        public float AlphaCutoff;
        public bool ThinFilm;
        public bool Subsurface;
        public float SubsurfaceRadius;
        public float[] SubsurfaceColor;
    }
    
    /// <summary>
    /// Remix light definition.
    /// </summary>
    public class RemixLightDef
    {
        public string Type;
        public float[] Position;
        public float[] Direction;
        public float[] Color;
        public float Intensity;
        public float Radius;
        public float ConeAngle;
        public float ConeSoftness;
    }
}


using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.TPC;
using Andastra.Parsing.Installation;
using Andastra.Parsing.Resource;
using Andastra.Runtime.MonoGame.Converters;
using Andastra.Runtime.MonoGame.Enums;
using Andastra.Runtime.MonoGame.Interfaces;
using JetBrains.Annotations;

namespace Andastra.Runtime.MonoGame.Materials
{
    /// <summary>
    /// Factory for creating PBR materials from KOTOR material data.
    /// 
    /// Material Factory Implementation:
    /// - Based on swkotor.exe and swkotor2.exe material initialization system
    /// - Located via string references: "glMaterialfv" @ swkotor.exe:0x0078c234, swkotor2.exe:0x0080ad74
    /// - "glColorMaterial" @ swkotor.exe:0x0078c244, swkotor2.exe:0x0080ad84
    /// - "glBindMaterialParameterEXT" @ swkotor.exe:0x0073f75c, swkotor2.exe:0x007b77b0
    /// - Original implementation: KOTOR uses OpenGL fixed-function pipeline with glMaterialfv for material properties
    /// - Material loading: Materials loaded from MDL file format, textures loaded from TPC files
    /// - Texture loading: Uses resource system to load TPC files from installation (chitin.bif, texture packs, override)
    /// - Material properties: Diffuse, specular (color + power), self-illumination, environment maps, lightmaps
    /// - This implementation: Converts Blinn-Phong materials to modern PBR workflow
    /// - Material caching: Caches materials by name to avoid redundant loading
    /// - Texture caching: Caches texture handles to avoid redundant texture creation
    /// - Module preloading: Preloads all materials for a module to reduce runtime loading
    /// </summary>
    public class PbrMaterialFactory : IPbrMaterialFactory, IDisposable
    {
        private readonly IGraphicsBackend _backend;
        private readonly Installation _installation;
        private readonly Dictionary<string, IPbrMaterial> _materialCache;
        private readonly Dictionary<string, IntPtr> _textureCache;
        private bool _disposed;

        /// <summary>
        /// Creates a new PBR material factory.
        /// </summary>
        /// <param name="backend">Graphics backend for creating textures.</param>
        /// <param name="installation">Game installation for loading resources.</param>
        public PbrMaterialFactory([NotNull] IGraphicsBackend backend, [NotNull] Installation installation)
        {
            if (backend == null)
            {
                throw new ArgumentNullException(nameof(backend));
            }
            if (installation == null)
            {
                throw new ArgumentNullException(nameof(installation));
            }

            _backend = backend;
            _installation = installation;
            _materialCache = new Dictionary<string, IPbrMaterial>(StringComparer.OrdinalIgnoreCase);
            _textureCache = new Dictionary<string, IntPtr>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Creates a new PBR material.
        /// </summary>
        public IPbrMaterial Create(string name, MaterialType type)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Material name cannot be null or empty", nameof(name));
            }

            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PbrMaterialFactory));
            }

            // Check cache first
            string cacheKey = name.ToLowerInvariant();
            if (_materialCache.TryGetValue(cacheKey, out IPbrMaterial cached))
            {
                return cached;
            }

            // Create new material
            var material = new PbrMaterial(name, type);
            _materialCache[cacheKey] = material;

            return material;
        }

        /// <summary>
        /// Creates a material from KOTOR MDL material data.
        /// </summary>
        public IPbrMaterial CreateFromKotorMaterial(string name, KotorMaterialData data)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Material name cannot be null or empty", nameof(name));
            }

            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PbrMaterialFactory));
            }

            // Check cache first
            string cacheKey = name.ToLowerInvariant();
            if (_materialCache.TryGetValue(cacheKey, out IPbrMaterial cached))
            {
                return cached;
            }

            // Convert KOTOR material data to PBR material using converter
            var material = KotorMaterialConverter.Convert(name, data);

            // Load textures from installation
            LoadMaterialTextures(material, data);

            // Cache the material
            _materialCache[cacheKey] = material;

            return material;
        }

        /// <summary>
        /// Gets a cached material by name.
        /// </summary>
        public IPbrMaterial GetCached(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            if (_disposed)
            {
                return null;
            }

            string cacheKey = name.ToLowerInvariant();
            if (_materialCache.TryGetValue(cacheKey, out IPbrMaterial material))
            {
                return material;
            }

            return null;
        }

        /// <summary>
        /// Preloads all materials for a module.
        /// 
        /// Based on swkotor2.exe module material loading system
        /// Located via string references: "glMaterialfv" @ swkotor2.exe:0x0080ad74
        /// Original implementation: Materials are loaded on-demand when models are rendered
        /// - Material data is stored in MDL file nodes (textures, colors, properties)
        /// - Materials are initialized via glMaterialfv calls when rendering models
        /// - This implementation: Preloads all materials from MDL files in the module to reduce runtime loading
        /// 
        /// Implementation details:
        /// - Iterates through all MDL files in the module using Module.Models()
        /// - Loads MDL data from installation for each model
        /// - Extracts material information from MDL nodes (textures, colors, properties)
        /// - Creates materials using CreateFromKotorMaterial and caches them
        /// - Materials are created with model name prefix to ensure uniqueness
        /// </summary>
        public void PreloadModule(string moduleName)
        {
            if (string.IsNullOrEmpty(moduleName))
            {
                return;
            }

            if (_disposed)
            {
                return;
            }

            try
            {
                // Create Module instance to access module resources
                // Module files can be .rim, .mod, or .erf formats
                bool useDotMod = false;
                try
                {
                    string modulesPath = Installation.GetModulesPath(_installation.Path);
                    if (System.IO.Directory.Exists(modulesPath))
                    {
                        string[] moduleFiles = System.IO.Directory.GetFiles(modulesPath, moduleName + ".*");
                        foreach (string file in moduleFiles)
                        {
                            string ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
                            if (ext == ".mod")
                            {
                                useDotMod = true;
                                break;
                            }
                            else if (ext == ".rim" || ext == ".erf")
                            {
                                useDotMod = false;
                                break;
                            }
                        }
                    }
                }
                catch
                {
                    // Default to .rim format if we can't determine
                    useDotMod = false;
                }

                var module = new Module(moduleName, _installation, useDotMod: useDotMod);

                // Get all MDL model resources from the module
                List<ModuleResource> mdlModels = module.Models();

                if (mdlModels == null || mdlModels.Count == 0)
                {
                    Console.WriteLine("[PbrMaterialFactory] No MDL models found in module: " + moduleName);
                    return;
                }

                Console.WriteLine("[PbrMaterialFactory] Preloading materials from " + mdlModels.Count + " MDL models in module: " + moduleName);

                int preloadedCount = 0;
                int errorCount = 0;

                // Process each MDL model
                foreach (ModuleResource mdlResource in mdlModels)
                {
                    if (mdlResource == null)
                    {
                        continue;
                    }

                    try
                    {
                        // Get MDL data from module resource
                        byte[] mdlData = null;
                        if (mdlResource is ModuleResource<object> typedResource)
                        {
                            mdlData = typedResource.Data();
                        }
                        else
                        {
                            // Fallback: try to get data using installation
                            string resRef = mdlResource.GetResName();
                            ResourceResult result = _installation.Resource(resRef, ResourceType.MDL, null, moduleName);
                            if (result != null && result.Data != null)
                            {
                                mdlData = result.Data;
                            }
                        }

                        if (mdlData == null || mdlData.Length == 0)
                        {
                            Console.WriteLine("[PbrMaterialFactory] Failed to load MDL data for: " + mdlResource.GetResName());
                            errorCount++;
                            continue;
                        }

                        // Extract materials from MDL data
                        HashSet<MaterialInfo> materials = ExtractMaterialsFromMdl(mdlData);

                        // Create materials from extracted data
                        string modelName = mdlResource.GetResName();
                        foreach (MaterialInfo matInfo in materials)
                        {
                            try
                            {
                                // Create unique material name: "{modelName}_{materialIndex}"
                                string materialName = modelName + "_" + matInfo.Index;

                                // Check if already cached (may have been loaded previously)
                                if (_materialCache.ContainsKey(materialName.ToLowerInvariant()))
                                {
                                    continue;
                                }

                                // Convert to KotorMaterialData
                                KotorMaterialData kotorData = new KotorMaterialData
                                {
                                    DiffuseMap = matInfo.DiffuseMap,
                                    BumpMap = matInfo.BumpMap,
                                    EnvironmentMap = matInfo.EnvironmentMap,
                                    LightmapMap = matInfo.LightmapMap,
                                    DiffuseColor = matInfo.DiffuseColor,
                                    AmbientColor = matInfo.AmbientColor,
                                    SpecularColor = matInfo.SpecularColor,
                                    SpecularPower = matInfo.SpecularPower,
                                    SelfIllumColor = new Vector3(0.0f, 0.0f, 0.0f), // Default no self-illumination
                                    Alpha = matInfo.DiffuseColor.W // Use diffuse alpha
                                };

                                // Create and cache the material
                                CreateFromKotorMaterial(materialName, kotorData);
                                preloadedCount++;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("[PbrMaterialFactory] Failed to create material from MDL: " + modelName + ", error: " + ex.Message);
                                errorCount++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[PbrMaterialFactory] Failed to process MDL: " + mdlResource.GetResName() + ", error: " + ex.Message);
                        errorCount++;
                    }
                }

                Console.WriteLine("[PbrMaterialFactory] Preloaded " + preloadedCount + " materials from module: " + moduleName);
                if (errorCount > 0)
                {
                    Console.WriteLine("[PbrMaterialFactory] Encountered " + errorCount + " errors during material preloading");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[PbrMaterialFactory] Failed to preload materials for module: " + moduleName + ", error: " + ex.Message);
            }
        }

        /// <summary>
        /// Material information extracted from MDL node.
        /// </summary>
        private struct MaterialInfo
        {
            public int Index;
            public string DiffuseMap;
            public string BumpMap;
            public string EnvironmentMap;
            public string LightmapMap;
            public Vector4 DiffuseColor;
            public Vector3 AmbientColor;
            public Vector3 SpecularColor;
            public float SpecularPower;
        }

        /// <summary>
        /// Extracts material information from MDL binary data.
        /// 
        /// Based on swkotor2.exe MDL material structure
        /// MDL file format: Materials are stored in trimesh nodes (nodeId & 32 != 0)
        /// Material data offsets (from node start):
        /// - Texture0 (diffuse): offset 168, 32 bytes (ASCII string)
        /// - Lightmap: offset 200, 32 bytes (ASCII string)
        /// - Material properties (colors): Various offsets in node structure
        /// 
        /// Implementation:
        /// - Traverses MDL node tree starting from root node
        /// - Extracts material data from trimesh nodes
        /// - Collects unique materials by texture combinations
        /// - Returns set of MaterialInfo structures with material properties
        /// </summary>
        private HashSet<MaterialInfo> ExtractMaterialsFromMdl(byte[] mdlData)
        {
            var materials = new HashSet<MaterialInfo>();
            var materialSet = new HashSet<string>(); // For deduplication

            if (mdlData == null || mdlData.Length < 180)
            {
                return materials;
            }

            try
            {
                using (var reader = RawBinaryReader.FromBytes(mdlData, 12))
                {
                    // Read root node offset (at offset 168 from file start, which is 156 from position 12)
                    reader.Seek(156);
                    uint rootOffset = reader.ReadUInt32();

                    if (rootOffset == 0 || rootOffset >= (uint)mdlData.Length)
                    {
                        return materials;
                    }

                    // Traverse node tree to find trimesh nodes (which contain materials)
                    Stack<uint> nodes = new Stack<uint>();
                    nodes.Push(rootOffset);

                    int materialIndex = 0;

                    while (nodes.Count > 0)
                    {
                        uint nodeOffset = nodes.Pop();

                        if (nodeOffset == 0 || nodeOffset >= (uint)mdlData.Length)
                        {
                            continue;
                        }

                        reader.Seek((int)nodeOffset);

                        // Read node ID
                        uint nodeId = reader.ReadUInt32();

                        // Read child offsets
                        reader.Seek((int)nodeOffset + 44);
                        uint childOffsetsOffset = reader.ReadUInt32();
                        uint childOffsetsCount = reader.ReadUInt32();

                        // Push children onto stack for traversal
                        if (childOffsetsOffset > 0 && childOffsetsOffset < (uint)mdlData.Length && childOffsetsCount > 0)
                        {
                            reader.Seek((int)childOffsetsOffset);
                            Stack<uint> childOffsets = new Stack<uint>();
                            for (uint i = 0; i < childOffsetsCount && (childOffsetsOffset + i * 4) < mdlData.Length; i++)
                            {
                                uint childOffset = reader.ReadUInt32();
                                if (childOffset > 0 && childOffset < (uint)mdlData.Length)
                                {
                                    childOffsets.Push(childOffset);
                                }
                            }
                            while (childOffsets.Count > 0)
                            {
                                nodes.Push(childOffsets.Pop());
                            }
                        }

                        // Check if this is a trimesh node (bit 5 set in nodeId)
                        if ((nodeId & 32) != 0)
                        {
                            // Extract material information from trimesh node
                            MaterialInfo matInfo = new MaterialInfo
                            {
                                Index = materialIndex++
                            };

                            // Read texture0 (diffuse map) at offset 168 from node start
                            if (nodeOffset + 168 + 32 <= mdlData.Length)
                            {
                                reader.Seek((int)nodeOffset + 168);
                                string texture0 = reader.ReadString(32, "ascii", "ignore").Trim();
                                if (!string.IsNullOrEmpty(texture0) && texture0.ToUpperInvariant() != "NULL" && texture0.ToLowerInvariant() != "dirt")
                                {
                                    matInfo.DiffuseMap = texture0.ToLowerInvariant();
                                }
                            }

                            // Read lightmap at offset 200 from node start
                            if (nodeOffset + 200 + 32 <= mdlData.Length)
                            {
                                reader.Seek((int)nodeOffset + 200);
                                string lightmap = reader.ReadString(32, "ascii", "ignore").Trim();
                                if (!string.IsNullOrEmpty(lightmap) && lightmap.ToUpperInvariant() != "NULL")
                                {
                                    matInfo.LightmapMap = lightmap.ToLowerInvariant();
                                }
                            }

                            // Read material colors
                            // Diffuse color: typically at offset 104 (Vector3, 12 bytes)
                            if (nodeOffset + 104 + 12 <= mdlData.Length)
                            {
                                reader.Seek((int)nodeOffset + 104);
                                float r = reader.ReadSingle();
                                float g = reader.ReadSingle();
                                float b = reader.ReadSingle();
                                matInfo.DiffuseColor = new Vector4(r, g, b, 1.0f);
                                matInfo.AmbientColor = new Vector3(r * 0.3f, g * 0.3f, b * 0.3f); // Estimate ambient from diffuse
                            }
                            else
                            {
                                // Default colors
                                matInfo.DiffuseColor = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
                                matInfo.AmbientColor = new Vector3(0.3f, 0.3f, 0.3f);
                            }

                            // Default specular values (KOTOR materials typically don't have strong specular)
                            matInfo.SpecularColor = new Vector3(0.5f, 0.5f, 0.5f);
                            matInfo.SpecularPower = 32.0f;

                            // Create unique key for material deduplication
                            string materialKey = (matInfo.DiffuseMap ?? "") + "|" +
                                                (matInfo.LightmapMap ?? "") + "|" +
                                                matInfo.DiffuseColor.ToString();

                            if (!materialSet.Contains(materialKey))
                            {
                                materialSet.Add(materialKey);
                                materials.Add(matInfo);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[PbrMaterialFactory] Error extracting materials from MDL: " + ex.Message);
            }

            return materials;
        }

        /// <summary>
        /// Loads textures for a material from KOTOR material data.
        /// </summary>
        private void LoadMaterialTextures(PbrMaterial material, KotorMaterialData data)
        {
            // Load diffuse/albedo texture
            if (!string.IsNullOrEmpty(data.DiffuseMap))
            {
                IntPtr albedoHandle = LoadTexture(data.DiffuseMap);
                if (albedoHandle != IntPtr.Zero)
                {
                    material.AlbedoTexture = albedoHandle;
                }
            }

            // Load normal/bump map texture
            if (!string.IsNullOrEmpty(data.BumpMap))
            {
                IntPtr normalHandle = LoadTexture(data.BumpMap);
                if (normalHandle != IntPtr.Zero)
                {
                    material.NormalTexture = normalHandle;
                }
            }

            // Load environment map texture
            if (!string.IsNullOrEmpty(data.EnvironmentMap))
            {
                IntPtr envHandle = LoadTexture(data.EnvironmentMap);
                if (envHandle != IntPtr.Zero)
                {
                    material.EnvironmentTexture = envHandle;
                }
            }

            // Load lightmap texture
            if (!string.IsNullOrEmpty(data.LightmapMap))
            {
                IntPtr lightmapHandle = LoadTexture(data.LightmapMap);
                if (lightmapHandle != IntPtr.Zero)
                {
                    material.LightmapTexture = lightmapHandle;
                }
            }
        }

        /// <summary>
        /// Loads a texture from the installation and creates a backend texture handle.
        /// </summary>
        private IntPtr LoadTexture(string textureName)
        {
            if (string.IsNullOrEmpty(textureName))
            {
                return IntPtr.Zero;
            }

            // Check texture cache
            string cacheKey = textureName.ToLowerInvariant();
            if (_textureCache.TryGetValue(cacheKey, out IntPtr cachedHandle))
            {
                return cachedHandle;
            }

            // Load TPC from installation
            TPC tpc = _installation.Texture(textureName);
            if (tpc == null)
            {
                Console.WriteLine("[PbrMaterialFactory] Failed to load texture: " + textureName);
                return IntPtr.Zero;
            }

            // Convert TPC to RGBA data
            byte[] rgbaData = TpcToMonoGameTextureConverter.ConvertToRgba(tpc);
            if (rgbaData == null || rgbaData.Length == 0)
            {
                Console.WriteLine("[PbrMaterialFactory] Failed to convert texture to RGBA: " + textureName);
                return IntPtr.Zero;
            }

            // Get texture dimensions from TPC
            if (tpc.Layers.Count == 0 || tpc.Layers[0].Mipmaps.Count == 0)
            {
                Console.WriteLine("[PbrMaterialFactory] TPC has no texture data: " + textureName);
                return IntPtr.Zero;
            }

            TPCMipmap baseMipmap = tpc.Layers[0].Mipmaps[0];
            int width = baseMipmap.Width;
            int height = baseMipmap.Height;
            int mipLevels = tpc.Layers[0].Mipmaps.Count;

            // Create texture description for backend
            TextureDescription desc = new TextureDescription
            {
                Width = width,
                Height = height,
                Depth = 1,
                MipLevels = mipLevels,
                ArraySize = 1,
                Format = TextureFormat.R8G8B8A8_UNorm,
                Usage = TextureUsage.ShaderResource,
                IsCubemap = false,
                SampleCount = 1,
                DebugName = textureName
            };

            // Create texture using backend
            IntPtr textureHandle = _backend.CreateTexture(desc);

            if (textureHandle == IntPtr.Zero)
            {
                Console.WriteLine("[PbrMaterialFactory] Backend failed to create texture: " + textureName);
                return IntPtr.Zero;
            }

            // Upload texture data to backend
            // Matches original engine behavior: swkotor.exe and swkotor2.exe create texture objects
            // and then upload pixel data using glTexImage2D/glCompressedTexImage2D for each mipmap level
            // This implementation follows the same pattern: create texture, then upload data
            TextureMipmapData[] mipmapData = new TextureMipmapData[mipLevels];
            for (int mip = 0; mip < mipLevels; mip++)
            {
                TPCMipmap tpcMipmap = tpc.Layers[0].Mipmaps[mip];
                int mipWidth = tpcMipmap.Width;
                int mipHeight = tpcMipmap.Height;

                // Convert this mipmap level to RGBA
                // ConvertMipmapToRgba handles all TPC formats: DXT1/DXT3/DXT5, RGBA, BGRA, RGB, grayscale
                byte[] mipRgbaData = null;
                try
                {
                    mipRgbaData = TpcToMonoGameTextureConverter.ConvertMipmapToRgba(tpcMipmap);
                    if (mipRgbaData == null || mipRgbaData.Length == 0)
                    {
                        Console.WriteLine($"[PbrMaterialFactory] Failed to convert mipmap {mip} to RGBA: " + textureName);
                        // Create empty mipmap data as fallback
                        mipRgbaData = new byte[mipWidth * mipHeight * 4];
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PbrMaterialFactory] Exception converting mipmap {mip} to RGBA: {ex.Message}");
                    // Create empty mipmap data as fallback
                    mipRgbaData = new byte[mipWidth * mipHeight * 4];
                }

                mipmapData[mip] = new TextureMipmapData
                {
                    Level = mip,
                    Width = mipWidth,
                    Height = mipHeight,
                    Data = mipRgbaData
                };
            }

            TextureUploadData uploadData = new TextureUploadData
            {
                Mipmaps = mipmapData,
                Format = TextureFormat.R8G8B8A8_UNorm
            };

            bool uploadSuccess = _backend.UploadTextureData(textureHandle, uploadData);
            if (!uploadSuccess)
            {
                Console.WriteLine("[PbrMaterialFactory] Failed to upload texture data: " + textureName);
                // Don't fail completely - texture handle is still valid, data may be uploaded later
            }

            // Cache the texture handle
            _textureCache[cacheKey] = textureHandle;

            Console.WriteLine("[PbrMaterialFactory] Loaded texture: " + textureName + " (" + width + "x" + height + ", " + mipLevels + " mipmaps)");

            return textureHandle;
        }

        /// <summary>
        /// Disposes the factory and releases resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            // Dispose all cached materials
            foreach (var material in _materialCache.Values)
            {
                material?.Dispose();
            }
            _materialCache.Clear();

            // Note: Texture handles are managed by the backend, not destroyed here
            // The backend will handle texture cleanup when it's disposed
            _textureCache.Clear();

            _disposed = true;
        }
    }
}


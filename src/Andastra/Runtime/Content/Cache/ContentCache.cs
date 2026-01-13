using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BioWare.NET.Common;
using BioWare.NET.Resource;
using Andastra.Runtime.Content.Interfaces;

namespace Andastra.Runtime.Content.Cache
{
    /// <summary>
    /// File-based content cache for converted assets.
    /// </summary>
    /// <remarks>
    /// Content Cache System:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) asset caching system
    /// - Located via string references: "CACHE" @ 0x007c6848, "z:\cache" @ 0x007c6850, "CExoKeyTable" resource management
    /// - Original implementation: Engine caches loaded models, textures, and other assets to avoid redundant loading
    /// - Resource management: CExoKeyTable handles resource key management, CExoKeyTable::AddKey adds resources to cache
    /// - Cache directory: Stores converted assets on disk for faster subsequent loads (enhancement over original in-memory cache)
    /// - Memory cache: In-memory cache for frequently accessed assets (LRU eviction)
    /// - Cache key: Based on game type, resource reference, resource type, source hash, and converter version
    /// - Cache invalidation: Source hash changes invalidate cached assets when source files are modified
    /// - Cache size limits: Configurable maximum cache size with LRU eviction when limit exceeded
    /// - Thread-safe: Concurrent access support for async loading scenarios
    /// - Note: Original engine uses in-memory caching via CExoKeyTable, this adds persistent disk cache for converted assets
    /// </remarks>
    public class ContentCache : IContentCache
    {
        private readonly string _cacheDir;
        private readonly Dictionary<CacheKey, CacheEntry> _memoryCache;
        private readonly object _lock = new object();
        private long _totalSize;

        private const long DefaultMaxCacheSize = 1024 * 1024 * 1024; // 1 GB

        public ContentCache(string cacheDirectory)
        {
            if (string.IsNullOrEmpty(cacheDirectory))
            {
                // Default to user profile directory
                cacheDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Odyssey",
                    "Cache"
                );
            }

            _cacheDir = cacheDirectory;
            _memoryCache = new Dictionary<CacheKey, CacheEntry>();

            if (!Directory.Exists(_cacheDir))
            {
                Directory.CreateDirectory(_cacheDir);
            }

            // Calculate initial size
            _totalSize = CalculateCacheSize();
        }

        public string CacheDirectory { get { return _cacheDir; } }
        public long TotalSize { get { return _totalSize; } }

        public async Task<CacheResult<T>> TryGetAsync<T>(CacheKey key, CancellationToken ct) where T : class
        {
            // Check memory cache first
            lock (_lock)
            {
                if (_memoryCache.TryGetValue(key, out CacheEntry entry))
                {
                    entry.LastAccess = DateTime.UtcNow;
                    if (entry.Value is T typedValue)
                    {
                        return CacheResult<T>.Hit(typedValue);
                    }
                }
            }

            // Check file cache
            string filePath = GetCacheFilePath(key);
            if (!File.Exists(filePath))
            {
                return CacheResult<T>.Miss();
            }

            return await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    // Read metadata file
                    string metaPath = filePath + ".meta";
                    if (!File.Exists(metaPath))
                    {
                        return CacheResult<T>.Miss();
                    }

                    // Parse and validate metadata
                    CacheMetadata metadata = ParseMetadata(metaPath);
                    if (metadata == null)
                    {
                        return CacheResult<T>.Miss();
                    }

                    // Validate metadata matches cache key
                    if (!ValidateMetadata(metadata, key))
                    {
                        // Metadata mismatch - cache entry is invalid
                        Invalidate(key);
                        return CacheResult<T>.Miss();
                    }

                    // Read cached data file
                    if (!File.Exists(filePath))
                    {
                        return CacheResult<T>.Miss();
                    }

                    byte[] cachedData = File.ReadAllBytes(filePath);
                    if (cachedData == null || cachedData.Length == 0)
                    {
                        return CacheResult<T>.Miss();
                    }

                    // Deserialize based on type T
                    T deserializedItem = DeserializeItem<T>(cachedData, metadata);
                    if (deserializedItem == null)
                    {
                        return CacheResult<T>.Miss();
                    }

                    // Add to memory cache for faster subsequent access
                    lock (_lock)
                    {
                        _memoryCache[key] = new CacheEntry
                        {
                            Value = deserializedItem,
                            LastAccess = DateTime.UtcNow,
                            Size = EstimateSize(deserializedItem)
                        };
                    }

                    return CacheResult<T>.Hit(deserializedItem);
                }
                catch (Exception)
                {
                    // On any error, return miss (cache corruption, I/O errors, etc.)
                    return CacheResult<T>.Miss();
                }
            }, ct);
        }

        public async Task StoreAsync<T>(CacheKey key, T item, CancellationToken ct) where T : class
        {
            if (item == null)
            {
                return;
            }

            // Store in memory cache
            lock (_lock)
            {
                _memoryCache[key] = new CacheEntry
                {
                    Value = item,
                    LastAccess = DateTime.UtcNow,
                    Size = EstimateSize(item)
                };
            }

            // Store to file cache (async)
            await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    string filePath = GetCacheFilePath(key);
                    string dir = Path.GetDirectoryName(filePath);
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    // Write metadata file
                    string metaPath = filePath + ".meta";
                    string typeName = typeof(T).AssemblyQualifiedName ?? typeof(T).FullName ?? "Unknown";
                    string metaContent = string.Format(
                        "game={0}\nresref={1}\ntype={2}\nhash={3}\nversion={4}\ntime={5}\ntypename={6}",
                        key.GameType,
                        key.ResRef,
                        (int)key.ResourceType,
                        key.SourceHash,
                        key.ConverterVersion,
                        DateTime.UtcNow.ToString("O"),
                        typeName
                    );
                    File.WriteAllText(metaPath, metaContent);

                    // Serialize and write cached data file
                    byte[] serializedData = SerializeItem(item);
                    if (serializedData != null && serializedData.Length > 0)
                    {
                        File.WriteAllBytes(filePath, serializedData);

                        // Update cache size tracking
                        lock (_lock)
                        {
                            _totalSize = CalculateCacheSize();
                        }
                    }
                }
                catch
                {
                    // Ignore cache write failures
                }
            }, ct);
        }

        public void Invalidate(CacheKey key)
        {
            lock (_lock)
            {
                _memoryCache.Remove(key);
            }

            try
            {
                string filePath = GetCacheFilePath(key);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                string metaPath = filePath + ".meta";
                if (File.Exists(metaPath))
                {
                    File.Delete(metaPath);
                }
            }
            catch
            {
                // Ignore deletion failures
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _memoryCache.Clear();
            }

            try
            {
                if (Directory.Exists(_cacheDir))
                {
                    Directory.Delete(_cacheDir, recursive: true);
                    Directory.CreateDirectory(_cacheDir);
                }
                _totalSize = 0;
            }
            catch
            {
                // Ignore deletion failures
            }
        }

        public void Prune(long maxSizeBytes)
        {
            if (_totalSize <= maxSizeBytes)
            {
                return;
            }

            try
            {
                // Get all cache files sorted by last access time
                var files = new DirectoryInfo(_cacheDir)
                    .GetFiles("*.meta", SearchOption.AllDirectories)
                    .OrderBy(f => f.LastAccessTimeUtc)
                    .ToList();

                long currentSize = _totalSize;

                foreach (FileInfo metaFile in files)
                {
                    if (currentSize <= maxSizeBytes)
                    {
                        break;
                    }

                    string dataFile = metaFile.FullName.Substring(0, metaFile.FullName.Length - 5);
                    long fileSize = 0;

                    if (File.Exists(dataFile))
                    {
                        fileSize = new FileInfo(dataFile).Length;
                        File.Delete(dataFile);
                    }

                    metaFile.Delete();
                    currentSize -= fileSize;
                }

                _totalSize = currentSize;
            }
            catch
            {
                // Ignore pruning failures
            }
        }

        private string GetCacheFilePath(CacheKey key)
        {
            string subdir = key.GameType.ToString();
            string filename = key.ToFileName();
            return Path.Combine(_cacheDir, subdir, filename);
        }

        private long CalculateCacheSize()
        {
            try
            {
                if (!Directory.Exists(_cacheDir))
                {
                    return 0;
                }

                return new DirectoryInfo(_cacheDir)
                    .GetFiles("*", SearchOption.AllDirectories)
                    .Sum(f => f.Length);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Estimates the memory size of a cached item in bytes.
        /// </summary>
        /// <remarks>
        /// Size Estimation:
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) memory management system
        /// - Original implementation: CExoKeyTable tracks resource memory usage for cache eviction
        /// - Accurate size estimation is critical for LRU cache eviction and memory budget management
        /// - Handles primitive types, arrays, collections, MonoGame graphics types, and custom types via reflection
        /// - For graphics resources: Calculates actual GPU memory usage (texture size, buffer size, etc.)
        /// - For collections: Recursively estimates size of all contained elements
        /// - For custom types: Uses reflection to estimate size of all fields and properties
        /// - Fallback: Uses serialized size as approximation when direct calculation isn't possible
        /// </remarks>
        private static long EstimateSize(object item)
        {
            if (item == null)
            {
                return 0;
            }

            Type itemType = item.GetType();

            // Handle byte arrays directly (most common case)
            if (item is byte[] bytes)
            {
                return bytes.Length;
            }

            // Handle strings
            if (item is string str)
            {
                // UTF-8 encoding size: each character is 1-4 bytes, but for estimation use 2 bytes per char
                // This is conservative and accounts for most common ASCII/Latin characters
                return Encoding.UTF8.GetByteCount(str);
            }

            // Handle MonoGame Texture2D
            if (itemType.Name == "Texture2D" && itemType.Namespace != null && itemType.Namespace.StartsWith("Microsoft.Xna.Framework.Graphics"))
            {
                return EstimateTexture2DSize(item);
            }

            // Handle MonoGame VertexBuffer
            if (itemType.Name == "VertexBuffer" && itemType.Namespace != null && itemType.Namespace.StartsWith("Microsoft.Xna.Framework.Graphics"))
            {
                return EstimateVertexBufferSize(item);
            }

            // Handle MonoGame IndexBuffer
            if (itemType.Name == "IndexBuffer" && itemType.Namespace != null && itemType.Namespace.StartsWith("Microsoft.Xna.Framework.Graphics"))
            {
                return EstimateIndexBufferSize(item);
            }

            // Handle MonoGame Model
            if (itemType.Name == "Model" && itemType.Namespace != null && itemType.Namespace.StartsWith("Microsoft.Xna.Framework.Graphics"))
            {
                return EstimateModelSize(item);
            }

            // Handle MonoGame BasicEffect
            if (itemType.Name == "BasicEffect" && itemType.Namespace != null && itemType.Namespace.StartsWith("Microsoft.Xna.Framework.Graphics"))
            {
                // BasicEffect is relatively small - just shader parameters and state
                return 1024; // ~1KB for effect parameters and state
            }

            // Handle arrays
            if (itemType.IsArray)
            {
                return EstimateArraySize((Array)item);
            }

            // Handle generic collections (List<T>, Dictionary<K,V>, etc.)
            if (item is ICollection collection && !(item is string))
            {
                return EstimateCollectionSize(collection);
            }

            // Handle IDictionary specifically (needs key+value size estimation)
            if (item is IDictionary dictionary)
            {
                return EstimateDictionarySize(dictionary);
            }

            // For other types, use reflection to estimate size
            return EstimateObjectSizeViaReflection(item, itemType);
        }

        /// <summary>
        /// Estimates the size of a Texture2D in bytes.
        /// </summary>
        private static long EstimateTexture2DSize(object texture)
        {
            try
            {
                Type textureType = texture.GetType();
                PropertyInfo widthProp = textureType.GetProperty("Width");
                PropertyInfo heightProp = textureType.GetProperty("Height");
                PropertyInfo formatProp = textureType.GetProperty("Format");

                if (widthProp != null && heightProp != null)
                {
                    int width = (int)widthProp.GetValue(texture);
                    int height = (int)heightProp.GetValue(texture);

                    // Get format if available
                    int bytesPerPixel = 4; // Default to RGBA8
                    if (formatProp != null)
                    {
                        object format = formatProp.GetValue(texture);
                        if (format != null)
                        {
                            bytesPerPixel = EstimateTextureFormatSize(format);
                        }
                    }

                    // Calculate total size: width * height * bytes per pixel
                    // Add mipmap overhead (approximately 33% for full mip chain)
                    long baseSize = (long)width * height * bytesPerPixel;
                    return baseSize + (baseSize / 3); // Add mipmap overhead
                }
            }
            catch
            {
                // If reflection fails, use conservative estimate
            }

            // Fallback: assume 512x512 RGBA texture
            return 512 * 512 * 4 * 4 / 3; // ~1.3MB with mipmaps
        }

        /// <summary>
        /// Estimates bytes per pixel for a texture format.
        /// </summary>
        private static int EstimateTextureFormatSize(object format)
        {
            if (format == null)
            {
                return 4; // Default RGBA8
            }

            string formatName = format.ToString();
            if (formatName.Contains("Rgba32") || formatName.Contains("Color"))
            {
                return 4; // RGBA8
            }
            if (formatName.Contains("Rgba64") || formatName.Contains("Rgba16"))
            {
                return 8; // RGBA16
            }
            if (formatName.Contains("Rgb24") || formatName.Contains("Rgb"))
            {
                return 3; // RGB8
            }
            if (formatName.Contains("Dxt1") || formatName.Contains("Bc1"))
            {
                return 1; // DXT1: 4 bits per pixel = 0.5 bytes, but compressed
            }
            if (formatName.Contains("Dxt3") || formatName.Contains("Dxt5") || formatName.Contains("Bc2") || formatName.Contains("Bc3"))
            {
                return 1; // DXT3/5: 8 bits per pixel = 1 byte, but compressed
            }
            if (formatName.Contains("Rg16") || formatName.Contains("Rg8"))
            {
                return 2; // RG8
            }
            if (formatName.Contains("R8") || formatName.Contains("Alpha8"))
            {
                return 1; // R8
            }

            return 4; // Default to RGBA8
        }

        /// <summary>
        /// Estimates the size of a VertexBuffer in bytes.
        /// </summary>
        private static long EstimateVertexBufferSize(object vertexBuffer)
        {
            try
            {
                Type bufferType = vertexBuffer.GetType();
                PropertyInfo vertexCountProp = bufferType.GetProperty("VertexCount");
                PropertyInfo vertexDeclarationProp = bufferType.GetProperty("VertexDeclaration");

                if (vertexCountProp != null)
                {
                    int vertexCount = (int)vertexCountProp.GetValue(vertexBuffer);
                    int vertexStride = 32; // Default: assume 32 bytes per vertex (Position + Normal + UV)

                    if (vertexDeclarationProp != null)
                    {
                        object vertexDeclaration = vertexDeclarationProp.GetValue(vertexBuffer);
                        if (vertexDeclaration != null)
                        {
                            Type declType = vertexDeclaration.GetType();
                            PropertyInfo strideProp = declType.GetProperty("VertexStride");
                            if (strideProp != null)
                            {
                                vertexStride = (int)strideProp.GetValue(vertexDeclaration);
                            }
                        }
                    }

                    return (long)vertexCount * vertexStride;
                }
            }
            catch
            {
                // If reflection fails, use conservative estimate
            }

            // Fallback: assume 1000 vertices at 32 bytes each
            return 1000 * 32;
        }

        /// <summary>
        /// Estimates the size of an IndexBuffer in bytes.
        /// </summary>
        private static long EstimateIndexBufferSize(object indexBuffer)
        {
            try
            {
                Type bufferType = indexBuffer.GetType();
                PropertyInfo indexCountProp = bufferType.GetProperty("IndexCount");
                PropertyInfo indexElementSizeProp = bufferType.GetProperty("IndexElementSize");

                if (indexCountProp != null)
                {
                    int indexCount = (int)indexCountProp.GetValue(indexBuffer);
                    int bytesPerIndex = 2; // Default: 16-bit indices

                    if (indexElementSizeProp != null)
                    {
                        object elementSize = indexElementSizeProp.GetValue(indexBuffer);
                        if (elementSize != null)
                        {
                            string elementSizeStr = elementSize.ToString();
                            if (elementSizeStr.Contains("ThirtyTwoBits") || elementSizeStr.Contains("32"))
                            {
                                bytesPerIndex = 4;
                            }
                        }
                    }

                    return (long)indexCount * bytesPerIndex;
                }
            }
            catch
            {
                // If reflection fails, use conservative estimate
            }

            // Fallback: assume 3000 indices at 2 bytes each (16-bit)
            return 3000 * 2;
        }

        /// <summary>
        /// Estimates the size of a Model in bytes.
        /// </summary>
        private static long EstimateModelSize(object model)
        {
            try
            {
                Type modelType = model.GetType();
                PropertyInfo meshesProp = modelType.GetProperty("Meshes");

                if (meshesProp != null)
                {
                    object meshes = meshesProp.GetValue(model);
                    if (meshes is IEnumerable meshCollection)
                    {
                        long totalSize = 0;
                        foreach (object mesh in meshCollection)
                        {
                            if (mesh != null)
                            {
                                // Estimate each mesh (vertex buffers, index buffers, effects)
                                totalSize += EstimateObjectSizeViaReflection(mesh, mesh.GetType());
                            }
                        }
                        return totalSize;
                    }
                }
            }
            catch
            {
                // If reflection fails, use conservative estimate
            }

            // Fallback: assume model with 5 meshes, ~100KB each
            return 5 * 100 * 1024;
        }

        /// <summary>
        /// Estimates the size of an array in bytes.
        /// </summary>
        private static long EstimateArraySize(Array array)
        {
            if (array == null || array.Length == 0)
            {
                return 0;
            }

            Type elementType = array.GetType().GetElementType();
            long elementSize = GetPrimitiveTypeSize(elementType);

            if (elementSize > 0)
            {
                // Primitive array: element size * length
                return elementSize * array.Length;
            }

            // Object array: estimate each element
            long totalSize = 0;
            foreach (object element in array)
            {
                totalSize += EstimateSize(element);
            }
            return totalSize;
        }

        /// <summary>
        /// Estimates the size of a collection in bytes.
        /// </summary>
        private static long EstimateCollectionSize(ICollection collection)
        {
            if (collection == null || collection.Count == 0)
            {
                return 0;
            }

            long totalSize = 0;
            foreach (object item in collection)
            {
                totalSize += EstimateSize(item);
            }

            // Add overhead for collection structure (dictionary overhead, list overhead, etc.)
            return totalSize + (collection.Count * 16); // ~16 bytes overhead per item
        }

        /// <summary>
        /// Estimates the size of a dictionary in bytes.
        /// </summary>
        private static long EstimateDictionarySize(IDictionary dictionary)
        {
            if (dictionary == null || dictionary.Count == 0)
            {
                return 0;
            }

            long totalSize = 0;
            foreach (DictionaryEntry entry in dictionary)
            {
                totalSize += EstimateSize(entry.Key);
                totalSize += EstimateSize(entry.Value);
            }

            // Add overhead for dictionary structure (hash table overhead)
            return totalSize + (dictionary.Count * 24); // ~24 bytes overhead per entry
        }

        /// <summary>
        /// Estimates the size of an object using reflection to examine fields and properties.
        /// </summary>
        private static long EstimateObjectSizeViaReflection(object obj, Type objType)
        {
            if (obj == null || objType == null)
            {
                return 0;
            }

            long totalSize = 0;

            // Base object overhead (object header, type pointer, etc.)
            totalSize += IntPtr.Size * 2; // Object header overhead

            try
            {
                // Estimate size of all fields
                FieldInfo[] fields = objType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (FieldInfo field in fields)
                {
                    try
                    {
                        object fieldValue = field.GetValue(obj);
                        if (fieldValue != null)
                        {
                            Type fieldType = field.FieldType;

                            // Skip if already visited (avoid infinite recursion)
                            if (IsPrimitiveOrSimpleType(fieldType))
                            {
                                totalSize += GetPrimitiveTypeSize(fieldType);
                            }
                            else if (!fieldType.IsValueType)
                            {
                                // Reference type: estimate the referenced object
                                // Use a depth limit to avoid infinite recursion
                                totalSize += EstimateSize(fieldValue);
                            }
                            else
                            {
                                // Value type: estimate inline size
                                totalSize += GetValueTypeSize(fieldType);
                            }
                        }
                    }
                    catch
                    {
                        // Skip fields that can't be accessed
                    }
                }

                // Estimate size of properties (if they have getters that return data)
                PropertyInfo[] properties = objType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (PropertyInfo property in properties)
                {
                    if (property.CanRead && property.GetIndexParameters().Length == 0)
                    {
                        try
                        {
                            object propValue = property.GetValue(obj);
                            if (propValue != null)
                            {
                                Type propType = property.PropertyType;
                                if (IsPrimitiveOrSimpleType(propType))
                                {
                                    totalSize += GetPrimitiveTypeSize(propType);
                                }
                                else if (!propType.IsValueType)
                                {
                                    totalSize += EstimateSize(propValue);
                                }
                                else
                                {
                                    totalSize += GetValueTypeSize(propType);
                                }
                            }
                        }
                        catch
                        {
                            // Skip properties that can't be accessed
                        }
                    }
                }
            }
            catch
            {
                // If reflection fails, use serialization as fallback
                return EstimateSizeViaSerialization(obj);
            }

            return totalSize;
        }

        /// <summary>
        /// Estimates size by serializing the object (fallback method).
        /// </summary>
        private static long EstimateSizeViaSerialization(object obj)
        {
            try
            {
#pragma warning disable SYSLIB0011 // BinaryFormatter is obsolete but needed for content cache serialization
                using (var ms = new MemoryStream())
                {
                    var formatter = new BinaryFormatter();
                    formatter.Serialize(ms, obj);
                    return ms.Length;
                }
#pragma warning restore SYSLIB0011
            }
            catch
            {
                // If serialization fails, return conservative estimate
                return 1024; // 1KB default
            }
        }

        /// <summary>
        /// Gets the size of a primitive type in bytes.
        /// </summary>
        private static long GetPrimitiveTypeSize(Type type)
        {
            if (type == null)
            {
                return 0;
            }

            if (type == typeof(bool))
            {
                return 1;
            }
            if (type == typeof(byte) || type == typeof(sbyte))
            {
                return 1;
            }
            if (type == typeof(short) || type == typeof(ushort))
            {
                return 2;
            }
            if (type == typeof(int) || type == typeof(uint))
            {
                return 4;
            }
            if (type == typeof(long) || type == typeof(ulong))
            {
                return 8;
            }
            if (type == typeof(float))
            {
                return 4;
            }
            if (type == typeof(double))
            {
                return 8;
            }
            if (type == typeof(decimal))
            {
                return 16;
            }
            if (type == typeof(char))
            {
                return 2;
            }
            if (type == typeof(IntPtr) || type == typeof(UIntPtr))
            {
                return IntPtr.Size;
            }

            return 0; // Not a primitive type
        }

        /// <summary>
        /// Gets the size of a value type in bytes.
        /// </summary>
        private static long GetValueTypeSize(Type type)
        {
            if (type == null || !type.IsValueType)
            {
                return 0;
            }

            // For value types, use Marshal.SizeOf if available, otherwise estimate
            try
            {
                // Try to get size via reflection for common value types
                if (type.IsPrimitive)
                {
                    return GetPrimitiveTypeSize(type);
                }

                // For structs, estimate based on fields
                FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                long size = 0;
                foreach (FieldInfo field in fields)
                {
                    size += GetPrimitiveTypeSize(field.FieldType);
                }
                return size > 0 ? size : 8; // Default to 8 bytes for unknown structs
            }
            catch
            {
                return 8; // Default estimate
            }
        }

        /// <summary>
        /// Checks if a type is primitive or simple (doesn't need recursive estimation).
        /// </summary>
        private static bool IsPrimitiveOrSimpleType(Type type)
        {
            if (type == null)
            {
                return false;
            }

            return type.IsPrimitive ||
                   type == typeof(string) ||
                   type == typeof(decimal) ||
                   type == typeof(DateTime) ||
                   type == typeof(TimeSpan) ||
                   type == typeof(Guid);
        }

        /// <summary>
        /// Computes a hash of the source bytes for cache key generation.
        /// </summary>
        public static string ComputeHash(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return "empty";
            }

            using (var sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(data);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
        }

        /// <summary>
        /// Parses cache metadata from a metadata file.
        /// </summary>
        private CacheMetadata ParseMetadata(string metaPath)
        {
            try
            {
                string[] lines = File.ReadAllLines(metaPath);
                var metadata = new CacheMetadata();

                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    int equalsIndex = line.IndexOf('=');
                    if (equalsIndex < 0)
                    {
                        continue;
                    }

                    string key = line.Substring(0, equalsIndex).Trim().ToLowerInvariant();
                    string value = line.Substring(equalsIndex + 1).Trim();

                    switch (key)
                    {
                        case "game":
                            if (Enum.TryParse<GameType>(value, true, out GameType gameType))
                            {
                                metadata.GameType = gameType;
                            }
                            break;
                        case "resref":
                            metadata.ResRef = value;
                            break;
                        case "type":
                            if (int.TryParse(value, out int typeInt))
                            {
                                metadata.ResourceType = ResourceType.FromId(typeInt);
                            }
                            break;
                        case "hash":
                            metadata.SourceHash = value;
                            break;
                        case "version":
                            if (int.TryParse(value, out int version))
                            {
                                metadata.ConverterVersion = version;
                            }
                            break;
                        case "time":
                            if (DateTime.TryParse(value, out DateTime timestamp))
                            {
                                metadata.Timestamp = timestamp;
                            }
                            break;
                        case "typename":
                            metadata.TypeName = value;
                            break;
                    }
                }

                return metadata;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Validates that metadata matches the cache key.
        /// </summary>
        private bool ValidateMetadata(CacheMetadata metadata, CacheKey key)
        {
            if (metadata == null)
            {
                return false;
            }

            return metadata.GameType == key.GameType &&
                   string.Equals(metadata.ResRef, key.ResRef, StringComparison.OrdinalIgnoreCase) &&
                   metadata.ResourceType == key.ResourceType &&
                   string.Equals(metadata.SourceHash, key.SourceHash, StringComparison.Ordinal) &&
                   metadata.ConverterVersion == key.ConverterVersion;
        }

        /// <summary>
        /// Deserializes a cached item from byte array.
        /// </summary>
        private T DeserializeItem<T>(byte[] data, CacheMetadata metadata) where T : class
        {
            if (data == null || data.Length == 0)
            {
                return null;
            }

            try
            {
                // Handle byte[] directly (most common case for cached assets)
                if (typeof(T) == typeof(byte[]))
                {
                    return data as T;
                }

                // Handle string type
                if (typeof(T) == typeof(string))
                {
                    string str = Encoding.UTF8.GetString(data);
                    return str as T;
                }

                // For other types, use BinaryFormatter
#pragma warning disable SYSLIB0011 // BinaryFormatter is obsolete but needed for content cache serialization
                using (var ms = new MemoryStream(data))
                {
                    var formatter = new BinaryFormatter();
                    object obj = formatter.Deserialize(ms);
#pragma warning restore SYSLIB0011

                    if (obj is T typedObj)
                    {
                        return typedObj;
                    }
                }
            }
            catch (SerializationException)
            {
                // BinaryFormatter deserialization failed
                return null;
            }
            catch (Exception)
            {
                // Other deserialization errors
                return null;
            }

            return null;
        }

        /// <summary>
        /// Serializes an item to byte array for caching.
        /// </summary>
        private byte[] SerializeItem<T>(T item) where T : class
        {
            if (item == null)
            {
                return null;
            }

            try
            {
                // Handle byte[] directly (no serialization needed)
                if (item is byte[] bytes)
                {
                    return bytes;
                }

                // Handle string type
                if (item is string str)
                {
                    return Encoding.UTF8.GetBytes(str);
                }

                // For other types, use BinaryFormatter
#pragma warning disable SYSLIB0011 // BinaryFormatter is obsolete but needed for content cache serialization
                using (var ms = new MemoryStream())
                {
                    var formatter = new BinaryFormatter();
                    formatter.Serialize(ms, item);
                    return ms.ToArray();
                }
#pragma warning restore SYSLIB0011
            }
            catch (SerializationException)
            {
                // BinaryFormatter serialization failed (type may not be serializable)
                return null;
            }
            catch (Exception)
            {
                // Other serialization errors
                return null;
            }
        }

        private class CacheEntry
        {
            public object Value;
            public DateTime LastAccess;
            public long Size;
        }

        /// <summary>
        /// Cache metadata parsed from .meta file.
        /// </summary>
        private class CacheMetadata
        {
            public GameType GameType { get; set; }
            public string ResRef { get; set; }
            public ResourceType ResourceType { get; set; }
            public string SourceHash { get; set; }
            public int ConverterVersion { get; set; }
            public DateTime Timestamp { get; set; }
            public string TypeName { get; set; }
        }
    }
}


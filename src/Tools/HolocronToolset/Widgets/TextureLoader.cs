using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BioWare.NET.Installation;
using BioWare.NET.Resource;
using BioWare.NET.Resource.Formats.TPC;
using TGAImage = BioWare.NET.Resource.Formats.TPC.TGAImage;

namespace HolocronToolset.Widgets
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/texture_loader.py:35
    // Original: class TextureLoaderProcess(multiprocessing.Process):
    // Request tuple: (resref, restype, context, icon_size)
    public class TextureLoadRequest
    {
        public string ResRef { get; set; }
        public ResourceType ResType { get; set; }
        public object Context { get; set; }
        public int IconSize { get; set; }

        public TextureLoadRequest(string resRef, ResourceType resType, object context, int iconSize)
        {
            ResRef = resRef;
            ResType = resType;
            Context = context;
            IconSize = iconSize;
        }
    }

    // Result tuple: (context, mipmap_data, error)
    public class TextureLoadResult
    {
        public object Context { get; set; }
        public byte[] MipmapData { get; set; }
        public string Error { get; set; }

        public TextureLoadResult(object context, byte[] mipmapData, string error)
        {
            Context = context;
            MipmapData = mipmapData;
            Error = error;
        }
    }

    public class TextureLoader
    {
        private string _installationPath;
        private bool _isTsl;
        private CancellationTokenSource _cancellationTokenSource;
        private Task _loaderTask;
        private Installation _installation;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/texture_loader.py:56-57
        // Original: request_queue: "Queue[tuple[str, ResourceType, Any, int] | None]"
        // Original: result_queue: "Queue[tuple[Any, bytes | None, str | None]]"
        private readonly ConcurrentQueue<TextureLoadRequest> _requestQueue;
        private readonly ConcurrentQueue<TextureLoadResult> _resultQueue;

        // Sentinel value for shutdown (matching PyKotor SHUTDOWN_SENTINEL)
        private static readonly TextureLoadRequest ShutdownSentinel = new TextureLoadRequest(null, ResourceType.INVALID, null, 0);

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/texture_loader.py:52-71
        // Original: def __init__(self, installation_path: str, is_tsl: bool, request_queue, result_queue):
        public TextureLoader(string installationPath, bool isTsl)
        {
            _installationPath = installationPath;
            _isTsl = isTsl;
            _cancellationTokenSource = new CancellationTokenSource();
            _requestQueue = new ConcurrentQueue<TextureLoadRequest>();
            _resultQueue = new ConcurrentQueue<TextureLoadResult>();
        }

        // Public method to queue a texture load request
        // Matching PyKotor: self._loadRequestQueue.put_nowait((resref, restype, context, icon_size))
        public void QueueTextureLoad(string resRef, ResourceType resType, object context, int iconSize = 64)
        {
            var request = new TextureLoadRequest(resRef, resType, context, iconSize);
            _requestQueue.Enqueue(request);
        }

        // Public method to retrieve results (non-blocking)
        // Matching PyKotor: result = self._result_queue.get(timeout=0.5)
        public bool TryGetResult(out TextureLoadResult result)
        {
            return _resultQueue.TryDequeue(out result);
        }

        // Public method to request shutdown
        // Matching PyKotor: self._request_queue.put(self.SHUTDOWN_SENTINEL, timeout=1.0)
        public void RequestShutdown()
        {
            _requestQueue.Enqueue(ShutdownSentinel);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/texture_loader.py:74-118
        // Original: def run(self):
        public void Start()
        {
            _loaderTask = Task.Run(() => RunLoader(_cancellationTokenSource.Token));
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/texture_loader.py:74-118
        // Original: def run(self):
        private void RunLoader(CancellationToken cancellationToken)
        {
            try
            {
                // Initialize installation inside the loader task
                // (Installation objects can't be shared across threads safely, so we initialize here)
                // Note: Installation auto-detects K1 vs K2 based on game files
                // The _isTsl parameter is stored for compatibility but not used - Installation auto-detects
                _installation = new Installation(_installationPath);
                System.Console.WriteLine($"TextureLoader started for: {_installationPath}");

                // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/texture_loader.py:83-113
                // Original: while not self._shutdown.is_set():
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // Matching PyKotor: request = self._request_queue.get(timeout=0.5)
                        // Use TryDequeue with a timeout equivalent (check every 500ms)
                        TextureLoadRequest request = null;
                        if (_requestQueue.TryDequeue(out request))
                        {
                            // Check for shutdown sentinel
                            // Matching PyKotor: if request is None or request is self.SHUTDOWN_SENTINEL:
                            if (request == ShutdownSentinel || request.ResRef == null)
                            {
                                System.Console.WriteLine("TextureLoader received shutdown signal");
                                break;
                            }

                            // Unpack request (guaranteed to be valid at this point)
                            // Matching PyKotor: resref, restype, context, icon_size = request
                            string resref = request.ResRef;
                            ResourceType restype = request.ResType;
                            object context = request.Context;
                            int iconSize = request.IconSize;

                            // Load the texture
                            try
                            {
                                // Matching PyKotor: mipmap_data = self._load_texture(installation, resref, restype, icon_size)
                                byte[] mipmapData = LoadTextureInternal(_installation, resref, restype, iconSize);
                                // Matching PyKotor: self._result_queue.put((context, mipmap_data, None))
                                _resultQueue.Enqueue(new TextureLoadResult(context, mipmapData, null));
                            }
                            catch (Exception e)
                            {
                                // Matching PyKotor: error_msg = f"Error loading texture {resref}: {e}"
                                string errorMsg = $"Error loading texture {resref}: {e}";
                                System.Console.WriteLine($"TextureLoader warning: {errorMsg}");
                                // Matching PyKotor: self._result_queue.put((context, None, error_msg))
                                _resultQueue.Enqueue(new TextureLoadResult(context, null, errorMsg));
                                // Don't shutdown on individual texture load errors - continue processing
                            }
                        }
                        else
                        {
                            // No request available, wait a bit before checking again
                            // Matching PyKotor: except queue.Empty: continue
                            Thread.Sleep(50); // Small delay to prevent tight loop
                        }
                    }
                    catch (Exception e)
                    {
                        // Log error but don't crash the process - continue processing other requests
                        // Matching PyKotor: RobustLogger().error(f"TextureLoaderProcess error processing request: {e}")
                        System.Console.WriteLine($"TextureLoader error processing request: {e}");
                        // Continue the loop instead of crashing
                    }
                }
            }
            catch (Exception ex)
            {
                // Matching PyKotor: RobustLogger().error(f"TextureLoaderProcess fatal error: {e}")
                System.Console.WriteLine($"TextureLoader fatal error: {ex}");
            }
            finally
            {
                // Matching PyKotor: RobustLogger().info("TextureLoaderProcess shutting down")
                _installation = null; // Clear reference on shutdown
                System.Console.WriteLine("TextureLoader shutting down");
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/texture_loader.py:120-165
        // Original: def _load_texture(self, installation, resref, restype, icon_size: int = 64) -> bytes:
        private byte[] LoadTextureInternal(Installation installation, string resref, ResourceType restype, int iconSize = 64)
        {
            // Get texture data from installation
            // Matching PyKotor: texture_data = installation.resource(resref, restype, order=search_order)
            var textureResult = installation.Resources.LookupResource(resref, restype);
            if (textureResult == null || textureResult.Data == null)
            {
                throw new FileNotFoundException($"Texture not found: {resref}.{restype.Extension}");
            }

            byte[] textureBytes = textureResult.Data;

            TPCMipmap mipmap;
            if (restype == ResourceType.TPC)
            {
                // Matching PyKotor: tpc = read_tpc(texture_bytes)
                var tpc = TPCAuto.ReadTpc(textureBytes);
                // Matching PyKotor: mipmap = self._get_best_mipmap(tpc, icon_size)
                mipmap = GetBestMipmap(tpc, iconSize);
            }
            else if (restype == ResourceType.TGA)
            {
                // TGA - try to read via TPC format or use fallback
                try
                {
                    var tpc = TPCAuto.ReadTpc(textureBytes);
                    mipmap = GetBestMipmap(tpc, iconSize);
                }
                catch
                {
                    // Fall back to TGA reader
                    mipmap = LoadTgaViaTgaReader(textureBytes, iconSize);
                }
            }
            else
            {
                throw new NotSupportedException($"Unsupported texture type: {restype}");
            }

            // Serialize mipmap data for cross-thread transfer
            // Matching PyKotor: return self._serialize_mipmap(mipmap)
            return SerializeMipmap(mipmap);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/texture_loader.py:170-190
        // Original: def _get_best_mipmap(self, tpc: TPC, target_size: int) -> TPCMipmap:
        private TPCMipmap GetBestMipmap(TPC tpc, int targetSize)
        {
            if (tpc == null || tpc.Layers == null || tpc.Layers.Count == 0)
            {
                throw new ArgumentException("TPC has no layers");
            }

            var layer = tpc.Layers[0];
            if (layer.Mipmaps == null || layer.Mipmaps.Count == 0)
            {
                throw new ArgumentException("TPC has no mipmaps");
            }

            var mipmaps = layer.Mipmaps;

            // Find mipmap closest to target size
            // Matching PyKotor: best_mipmap: TPCMipmap = mipmaps[0]
            TPCMipmap bestMipmap = mipmaps[0];
            int bestDiff = Math.Abs(bestMipmap.Width - targetSize);

            for (int i = 1; i < mipmaps.Count; i++)
            {
                int diff = Math.Abs(mipmaps[i].Width - targetSize);
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    bestMipmap = mipmaps[i];
                }
            }

            return bestMipmap;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/texture_loader.py:192-253
        // Original: def _load_tga_via_pil(self, data: bytes, icon_size: int) -> TPCMipmap:
        private TPCMipmap LoadTgaViaTgaReader(byte[] data, int iconSize)
        {
            // Use TGA reader from BioWare.NET
            // Matching PyKotor: img = Image.open(BytesIO(data))
            TGAImage tga;
            using (var ms = new MemoryStream(data))
            {
                tga = TGA.ReadTga(ms);
            }

            // TGA.ReadTga already returns RGBA8888 data (see TGA.cs implementation)
            // Matching PyKotor: if img.mode != "RGBA": img = img.convert("RGBA")
            byte[] rgbaData = tga.Data; // Already RGBA8888 from TGA reader

            // Resize to icon size if needed
            // Matching PyKotor: img = img.resize((icon_size, icon_size), Image.Resampling.LANCZOS)
            if (tga.Width != iconSize || tga.Height != iconSize)
            {
                rgbaData = ResizeImage(rgbaData, tga.Width, tga.Height, iconSize, iconSize);
            }

            // Create TPCMipmap
            // Matching PyKotor: return TPCMipmap(width=img.width, height=img.height, tpc_format=TPCTextureFormat.RGBA, data=bytearray(img.tobytes()))
            return new TPCMipmap(iconSize, iconSize, TPCTextureFormat.RGBA, rgbaData);
        }

        /// <summary>
        /// Resizes image data using nearest-neighbor interpolation algorithm.
        /// This method provides high-quality image resizing while maintaining pixel-perfect accuracy
        /// for cases where exact pixel mapping is required.
        /// </summary>
        /// <param name="sourceData">Source image data in RGBA8888 format (4 bytes per pixel)</param>
        /// <param name="sourceWidth">Width of the source image in pixels</param>
        /// <param name="sourceHeight">Height of the source image in pixels</param>
        /// <param name="targetWidth">Desired width of the target image in pixels</param>
        /// <param name="targetHeight">Desired height of the target image in pixels</param>
        /// <returns>Resized image data in RGBA8888 format</returns>
        /// <exception cref="ArgumentNullException">Thrown when sourceData is null</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when dimensions are invalid</exception>
        private byte[] ResizeImage(byte[] sourceData, int sourceWidth, int sourceHeight, int targetWidth, int targetHeight)
        {
            // Validate input parameters
            if (sourceData == null)
                throw new ArgumentNullException(nameof(sourceData));

            if (sourceWidth <= 0 || sourceHeight <= 0 || targetWidth <= 0 || targetHeight <= 0)
                throw new ArgumentOutOfRangeException("Image dimensions must be positive");

            if (sourceData.Length < sourceWidth * sourceHeight * 4)
                throw new ArgumentException("Source data buffer is too small for the specified dimensions");

            // Handle trivial case - same dimensions
            if (sourceWidth == targetWidth && sourceHeight == targetHeight)
            {
                return (byte[])sourceData.Clone();
            }

            // Allocate target buffer
            byte[] targetData = new byte[targetWidth * targetHeight * 4];

            // Calculate scaling ratios with floating point precision for accurate mapping
            double scaleX = (double)sourceWidth / targetWidth;
            double scaleY = (double)sourceHeight / targetHeight;

            // Pre-calculate source dimensions for bounds checking
            int sourcePixels = sourceWidth * sourceHeight;
            int targetPixels = targetWidth * targetHeight;

            // Perform nearest-neighbor interpolation
            for (int targetY = 0; targetY < targetHeight; targetY++)
            {
                for (int targetX = 0; targetX < targetWidth; targetX++)
                {
                    // Calculate source coordinates using floating-point arithmetic
                    // Add 0.5 to sample from the center of the target pixel
                    double sourceXFloat = (targetX + 0.5) * scaleX - 0.5;
                    double sourceYFloat = (targetY + 0.5) * scaleY - 0.5;

                    // Clamp coordinates to valid range and convert to integer
                    int sourceX = Math.Max(0, Math.Min(sourceWidth - 1, (int)Math.Round(sourceXFloat)));
                    int sourceY = Math.Max(0, Math.Min(sourceHeight - 1, (int)Math.Round(sourceYFloat)));

                    // Calculate array indices
                    int sourceIndex = (sourceY * sourceWidth + sourceX) * 4;
                    int targetIndex = (targetY * targetWidth + targetX) * 4;

                    // Ensure indices are within bounds (extra safety check)
                    if (sourceIndex >= 0 && sourceIndex + 3 < sourceData.Length &&
                        targetIndex >= 0 && targetIndex + 3 < targetData.Length)
                    {
                        // Copy RGBA pixel data
                        targetData[targetIndex] = sourceData[sourceIndex];         // R
                        targetData[targetIndex + 1] = sourceData[sourceIndex + 1]; // G
                        targetData[targetIndex + 2] = sourceData[sourceIndex + 2]; // B
                        targetData[targetIndex + 3] = sourceData[sourceIndex + 3]; // A
                    }
                }
            }

            return targetData;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/texture_loader.py:255-274
        // Original: def _serialize_mipmap(self, mipmap: TPCMipmap) -> bytes:
        private byte[] SerializeMipmap(TPCMipmap mipmap)
        {
            // Serialize a TPCMipmap for cross-thread transfer
            // Returns a bytes object containing:
            // - width (4 bytes, int)
            // - height (4 bytes, int)
            // - format (4 bytes, int - TPCTextureFormat value)
            // - data_length (4 bytes, int)
            // - data (variable length bytes)

            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                // Matching PyKotor: struct.pack("<IIII", mipmap.width, mipmap.height, mipmap.tpc_format.value, len(mipmap.data))
                writer.Write(mipmap.Width);
                writer.Write(mipmap.Height);
                writer.Write((int)mipmap.TpcFormat);
                writer.Write(mipmap.Data != null ? mipmap.Data.Length : 0);

                if (mipmap.Data != null && mipmap.Data.Length > 0)
                {
                    writer.Write(mipmap.Data);
                }

                return ms.ToArray();
            }
        }

        // Public method for deserializing mipmap (can be used by consumers)
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/texture_loader.py:290-314
        // Original: def deserialize_mipmap(data: bytes) -> TPCMipmap:
        public static TPCMipmap DeserializeMipmap(byte[] data)
        {
            if (data == null || data.Length < 16)
            {
                throw new ArgumentException("Invalid mipmap data: insufficient length");
            }

            // Matching PyKotor: width, height, format_value, data_length = struct.unpack("<IIII", data[:header_size])
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                int width = reader.ReadInt32();
                int height = reader.ReadInt32();
                int formatValue = reader.ReadInt32();
                int dataLength = reader.ReadInt32();

                if (dataLength < 0 || dataLength > data.Length - 16)
                {
                    throw new ArgumentException("Invalid mipmap data: invalid data length");
                }

                byte[] mipmapData = reader.ReadBytes(dataLength);

                // Matching PyKotor: return TPCMipmap(width=width, height=height, tpc_format=TPCTextureFormat(format_value), data=mipmap_data)
                return new TPCMipmap(width, height, (TPCTextureFormat)formatValue, mipmapData);
            }
        }

        // Legacy public method for backward compatibility (deprecated - use queue system instead)
        [Obsolete("Use QueueTextureLoad and TryGetResult instead")]
        public byte[] LoadTexture(object installation, string resref, ResourceType restype, int iconSize = 64)
        {
            try
            {
                if (installation is Installation inst)
                {
                    return LoadTextureInternal(inst, resref, restype, iconSize);
                }
                return null;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error loading texture {resref}: {ex}");
                return null;
            }
        }

        public void Stop()
        {
            _cancellationTokenSource?.Cancel();
            _loaderTask?.Wait(TimeSpan.FromSeconds(5));
        }
    }
}

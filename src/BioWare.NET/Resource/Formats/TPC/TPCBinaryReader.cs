using System;
using System.IO;
using BioWare.NET.Resource.Formats.TXI;

namespace BioWare.NET.Resource.Formats.TPC
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/io_tpc.py:85-270
    // swkotor.exe/swkotor2.exe: TPC binary reader with full format conversions (deswizzling, cubemap normalization, animated texture handling)
    public class TPCBinaryReader : IDisposable
    {
        private readonly BioWare.NET.Common.RawBinaryReader _reader;
        private TPC _tpc;
        private int _layerCount;
        private int _mipmapCount;
        private const int MAX_DIMENSIONS = 0x8000;
        private const int IMG_DATA_START_OFFSET = 0x80;

        public TPCBinaryReader(byte[] data, int offset = 0, int size = 0)
        {
            _reader = BioWare.NET.Common.RawBinaryReader.FromBytes(data, offset, size > 0 ? size : (int?)null);
        }

        public TPCBinaryReader(string filepath, int offset = 0, int size = 0)
        {
            _reader = BioWare.NET.Common.RawBinaryReader.FromFile(filepath, offset, size > 0 ? size : (int?)null);
        }

        public TPCBinaryReader(Stream source, int offset = 0, int size = 0)
        {
            _reader = BioWare.NET.Common.RawBinaryReader.FromStream(source, offset, size > 0 ? size : (int?)null);
        }

        public TPC Load(bool autoClose = true)
        {
            try
            {
                _tpc = new TPC();
                _layerCount = 1;
                _mipmapCount = 0;

                int dataSize = (int)_reader.ReadUInt32();
                bool compressed = dataSize != 0;
                _tpc.AlphaTest = _reader.ReadSingle();
                int width = _reader.ReadUInt16();
                int height = _reader.ReadUInt16();

                // Validate dimensions
                if (Math.Max(width, height) >= MAX_DIMENSIONS)
                {
                    throw new ArgumentException($"Unsupported image dimensions ({width}x{height})");
                }

                byte pixelType = _reader.ReadUInt8();
                _mipmapCount = _reader.ReadUInt8();
                TPCTextureFormat format = TPCTextureFormat.Invalid;
                if (compressed)
                {
                    if (pixelType == 2)
                    {
                        format = TPCTextureFormat.DXT1;
                    }
                    else if (pixelType == 4)
                    {
                        format = TPCTextureFormat.DXT5;
                    }
                }
                else
                {
                    if (pixelType == 1)
                    {
                        format = TPCTextureFormat.Greyscale;
                    }
                    else if (pixelType == 2)
                    {
                        format = TPCTextureFormat.RGB;
                    }
                    else if (pixelType == 4)
                    {
                        format = TPCTextureFormat.RGBA;
                    }
                    else if (pixelType == 12)
                    {
                        format = TPCTextureFormat.BGRA;
                    }
                }
                if (format == TPCTextureFormat.Invalid)
                {
                    throw new ArgumentException($"Unsupported texture format (pixel_type: {pixelType}, compressed: {compressed})");
                }
                _tpc._format = format;

                const int totalCubeSides = 6;
                if (!compressed)
                {
                    dataSize = format.GetSize(width, height);
                }
                else if (height != 0 && width != 0 && (height / width) == totalCubeSides)
                {
                    _tpc.IsCubeMap = true;
                    height = height / totalCubeSides;
                    _layerCount = totalCubeSides;
                }

                int completeDataSize = dataSize;
                for (int level = 1; level < _mipmapCount; level++)
                {
                    int reducedWidth = Math.Max(width >> level, 1);
                    int reducedHeight = Math.Max(height >> level, 1);
                    completeDataSize += format.GetSize(reducedWidth, reducedHeight);
                }
                completeDataSize *= _layerCount;

                _reader.Skip(0x72 + completeDataSize);
                int txiSize = _reader.Size - _reader.Position;
                if (txiSize > 0)
                {
                    _tpc.Txi = _reader.ReadString(txiSize, System.Text.Encoding.ASCII.WebName);
                    _tpc.TxiObject = new TXI.TXI(_tpc.Txi);
                }

                // Detect animated textures from TXI data
                // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/io_tpc.py:169-193
                // swkotor.exe/swkotor2.exe: Animated texture detection via TXI proceduretype=cycle
                TXI.TXI txiData = _tpc.TxiObject;
                _tpc.IsAnimated = !string.IsNullOrWhiteSpace(_tpc.Txi) &&
                                  !string.IsNullOrWhiteSpace(txiData.Features.Proceduretype) &&
                                  txiData.Features.Proceduretype.ToLowerInvariant() == "cycle" &&
                                  txiData.Features.Numx.HasValue &&
                                  txiData.Features.Numx.Value != 0 &&
                                  txiData.Features.Numy.HasValue &&
                                  txiData.Features.Numy.Value != 0 &&
                                  txiData.Features.Fps.HasValue &&
                                  txiData.Features.Fps.Value != 0;

                if (_tpc.IsAnimated)
                {
                    // Adjust dimensions and layer count for animated textures
                    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/io_tpc.py:180-193
                    _layerCount = (txiData.Features.Numx.Value) * (txiData.Features.Numy.Value);
                    width /= txiData.Features.Numx.Value;
                    height /= txiData.Features.Numy.Value;
                    dataSize /= _layerCount;

                    // Recalculate mipmap count for animated textures
                    int animatedWidth = width;
                    int animatedHeight = height;
                    _mipmapCount = 0;
                    while (animatedWidth > 0 && animatedHeight > 0)
                    {
                        animatedWidth /= 2;
                        animatedHeight /= 2;
                        _mipmapCount++;
                    }
                }

                // Validate compressed texture data size
                // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/io_tpc.py:194-197
                if (compressed && !_tpc.IsAnimated)
                {
                    int expectedSize = (width * height) / 2;
                    if (format == TPCTextureFormat.DXT5)
                    {
                        expectedSize = width * height;
                    }
                    if (dataSize != expectedSize)
                    {
                        throw new ArgumentException($"Invalid data size for a texture of {width}x{height} pixels and format {format}");
                    }
                }

                _reader.Seek(IMG_DATA_START_OFFSET);
                if (width <= 0 || height <= 0 || width >= MAX_DIMENSIONS || height >= MAX_DIMENSIONS)
                {
                    throw new ArgumentException($"Invalid dimensions ({width}x{height}) for format {format}");
                }

                int fullImageDataSize = format.GetSize(width, height);
                int fullDataSize = _reader.Size - IMG_DATA_START_OFFSET;
                if (fullDataSize < (_layerCount * fullImageDataSize))
                {
                    throw new ArgumentException(
                        $"Insufficient data for image. Expected at least 0x{(_layerCount * fullImageDataSize):X} bytes, " +
                        $"but only 0x{fullDataSize:X} bytes are available.");
                }

                // Read texture data for each layer
                // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/io_tpc.py:217-255
                for (int layerIndex = 0; layerIndex < _layerCount; layerIndex++)
                {
                    TPCLayer layer = new TPCLayer();
                    _tpc.Layers.Add(layer);
                    int layerWidth = width;
                    int layerHeight = height;
                    int layerSize = _tpc.IsAnimated ? dataSize : format.GetSize(layerWidth, layerHeight);

                    for (int mip = 0; mip < _mipmapCount; mip++)
                    {
                        int mmWidth = Math.Max(1, layerWidth);
                        int mmHeight = Math.Max(1, layerHeight);
                        int mmSize = Math.Max(layerSize, format.MinSize());
                        byte[] data = _reader.ReadBytes(mmSize);
                        layer.Mipmaps.Add(new TPCMipmap(mmWidth, mmHeight, format, data));

                        if (fullDataSize <= mmSize || mmSize < format.GetSize(mmWidth, mmHeight))
                        {
                            break;
                        }

                        fullDataSize -= mmSize;
                        layerWidth >>= 1;
                        layerHeight >>= 1;
                        layerSize = format.GetSize(Math.Max(1, layerWidth), Math.Max(1, layerHeight));
                        if (layerWidth < 1 && layerHeight < 1)
                        {
                            break;
                        }
                    }
                }

                // Deswizzle BGRA format data
                // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/io_tpc.py:256-264
                // swkotor.exe/swkotor2.exe: BGRA textures are stored in swizzled format for GPU-friendly access
                if (_tpc.Format() == TPCTextureFormat.BGRA)
                {
                    foreach (var layer in _tpc.Layers)
                    {
                        foreach (var mipmap in layer.Mipmaps)
                        {
                            mipmap.Data = Deswizzle(
                                mipmap.Data,
                                mipmap.Width,
                                mipmap.Height,
                                _tpc.Format().BytesPerPixel()
                            );
                        }
                    }
                }

                // Normalize cubemaps (convert format, rotate layers, swap layers 0 and 1)
                // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/io_tpc.py:266-287
                // swkotor.exe/swkotor2.exe: Cubemap normalization for proper face orientation
                if (_tpc.IsCubeMap)
                {
                    NormalizeCubemaps();
                }

                return _tpc;
            }
            finally
            {
                if (autoClose)
                {
                    Dispose();
                }
            }
        }

        // Deswizzle pixel data from GPU-friendly layout to linear layout
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/io_tpc.py:55-81
        // swkotor.exe/swkotor2.exe: BGRA textures use swizzled memory layout for GPU cache optimization
        private static byte[] Deswizzle(byte[] data, int width, int height, int bytesPerPixel)
        {
            if (data == null || data.Length == 0)
            {
                return data;
            }

            byte[] deswizzled = new byte[width * height * bytesPerPixel];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int srcOffset = DeswizzleOffset(x, y, width, height) * bytesPerPixel;
                    int dstOffset = (y * width + x) * bytesPerPixel;
                    if (srcOffset + bytesPerPixel <= data.Length && dstOffset + bytesPerPixel <= deswizzled.Length)
                    {
                        Array.Copy(data, srcOffset, deswizzled, dstOffset, bytesPerPixel);
                    }
                }
            }
            return deswizzled;
        }

        // Calculate deswizzled offset for a given x, y coordinate
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/io_tpc.py:58-74
        private static int DeswizzleOffset(int x, int y, int w, int h)
        {
            int log2W = 0;
            int log2H = 0;
            int tempW = w;
            int tempH = h;
            while (tempW > 1)
            {
                log2W++;
                tempW >>= 1;
            }
            while (tempH > 1)
            {
                log2H++;
                tempH >>= 1;
            }

            int offset = 0;
            int shift = 0;
            while (log2W > 0 || log2H > 0)
            {
                if (log2W > 0)
                {
                    offset |= (x & 1) << shift;
                    x >>= 1;
                    shift++;
                    log2W--;
                }
                if (log2H > 0)
                {
                    offset |= (y & 1) << shift;
                    y >>= 1;
                    shift++;
                    log2H--;
                }
            }
            return offset;
        }

        // Normalize cubemaps: convert format, rotate layers, swap layers 0 and 1
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/io_tpc.py:271-287
        // swkotor.exe/swkotor2.exe: Cubemap face orientation normalization
        private void NormalizeCubemaps()
        {
            // Convert to RGB (if DXT1) or RGBA (if DXT5)
            TPCTextureFormat targetFormat = _tpc.Format() == TPCTextureFormat.DXT1
                ? TPCTextureFormat.RGB
                : TPCTextureFormat.RGBA;
            _tpc.Convert(targetFormat);

            // Rotation values for each cubemap face
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/io_tpc.py:273
            int[] rotation = new int[] { 3, 1, 0, 2, 2, 0 };
            for (int i = 0; i < _tpc.Layers.Count && i < rotation.Length; i++)
            {
                foreach (var mipmap in _tpc.Layers[i].Mipmaps)
                {
                    mipmap.Data = RotateRgbRgba(
                        mipmap.Data,
                        mipmap.Width,
                        mipmap.Height,
                        _tpc.Format().BytesPerPixel(),
                        rotation[i]
                    );
                }
            }

            // Swap layers 0 and 1
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/io_tpc.py:283-286
            if (_tpc.Layers.Count >= 2)
            {
                var temp = _tpc.Layers[0].Mipmaps;
                _tpc.Layers[0].Mipmaps = _tpc.Layers[1].Mipmaps;
                _tpc.Layers[1].Mipmaps = temp;
            }
        }

        // Rotate RGB/RGBA image data in 90° steps
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/manipulate/rotate.py:4-35
        // Used for cubemap normalization
        private static byte[] RotateRgbRgba(byte[] data, int width, int height, int bytesPerPixel, int times)
        {
            times = times % 4;
            if (times == 0)
            {
                return data;
            }

            byte[] newData = new byte[data.Length];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int srcIdx = (y * width + x) * bytesPerPixel;
                    int dstIdx;
                    if (times == 1 || times == -3)
                    {
                        dstIdx = ((width - 1 - x) * height + y) * bytesPerPixel;
                    }
                    else if (times == 2 || times == -2)
                    {
                        dstIdx = ((height - 1 - y) * width + (width - 1 - x)) * bytesPerPixel;
                    }
                    else if (times == 3 || times == -1)
                    {
                        dstIdx = (x * height + (height - 1 - y)) * bytesPerPixel;
                    }
                    else
                    {
                        dstIdx = srcIdx;
                    }

                    if (srcIdx + bytesPerPixel <= data.Length && dstIdx + bytesPerPixel <= newData.Length)
                    {
                        for (int i = 0; i < bytesPerPixel; i++)
                        {
                            newData[dstIdx + i] = data[srcIdx + i];
                        }
                    }
                }
            }
            return newData;
        }

        public void Dispose()
        {
            _reader?.Dispose();
        }
    }
}


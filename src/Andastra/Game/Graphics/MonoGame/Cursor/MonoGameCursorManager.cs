using System;
using System.Collections.Generic;
using System.IO;

using BioWare.NET.Extract.Installation;
using Andastra.Runtime.Graphics;
using JetBrains.Annotations;


namespace Andastra.Game.Graphics.MonoGame.Graphics.Cursor
{
    /// <summary>
    /// MonoGame implementation of ICursorManager.
    /// </summary>
    /// <remarks>
    /// MonoGame Cursor Manager:
    /// - Based on swkotor.exe and swkotor2.exe cursor management system
    /// - Cursor loading: Loads cursors from game resources or creates programmatic fallbacks
    /// - Cursor caching: Caches loaded cursors to avoid reloading
    /// - Cursor state: Tracks current cursor type and pressed state
    /// - Cursor position: Tracks mouse position for rendering
    /// - Cursor resources: Cursors are stored as Windows PE resources in EXE file (cursor groups 1, 2, 11, 12, etc.)
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Cursor groups are loaded from EXE PE resources, cursor resources are CUR format
    /// - Original implementation: FUN_00633270 @ 0x00633270 sets up resource directories, cursor resources loaded from EXE
    /// - Cursor group format: 4 bytes reserved, 2 bytes resCount, then for each cursor: 12 bytes (width, height, planes, bitCount, bytesInRes), 2 bytes cursorId
    /// - Cursor resource format: CUR file format with hotspot information and bitmap data
    /// </remarks>
    public class MonoGameCursorManager : ICursorManager
    {
        private readonly IGraphicsDevice _graphicsDevice;
        private readonly Dictionary<CursorType, ICursor> _cursorCache;
        [CanBeNull]
        private readonly Installation _installation;
        private CursorType _currentCursorType;
        private bool _isPressed;
        private Vector2 _position;
        private bool _disposed;

        /// <summary>
        /// Cursor group ID mappings for each cursor type.
        /// Maps CursorType to (upGroupId, downGroupId) pairs.
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe, swkotor2.exe: Cursor group IDs stored in EXE PE resources
        /// Based on reone/xoreos implementations: Cursor group ID mappings
        /// - Default: Groups 1, 2 (normal cursor)
        /// - Talk: Groups 11, 12 (when hovering over NPCs)
        /// - Door: Groups 23, 24 (when hovering over doors)
        /// - Pickup: Groups 25, 26 (when hovering over items)
        /// - Attack: Groups 51, 52 (when hovering over enemies)
        /// - DisableMine: Groups 33, 34 (when hovering over mines to disable)
        /// - RecoverMine: Groups 37, 38 (when hovering over mines to recover)
        /// </remarks>
        private static readonly Dictionary<CursorType, (int UpGroupId, int DownGroupId)> CursorGroupIds = new Dictionary<CursorType, (int, int)>
        {
            { CursorType.Default, (1, 2) },
            { CursorType.Talk, (11, 12) },
            { CursorType.Door, (23, 24) },
            { CursorType.Pickup, (25, 26) },
            { CursorType.Attack, (51, 52) }
        };

        public MonoGameCursorManager(IGraphicsDevice graphicsDevice, [CanBeNull] Installation installation = null)
        {
            _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException("graphicsDevice");
            _installation = installation;
            _cursorCache = new Dictionary<CursorType, ICursor>();
            _currentCursorType = CursorType.Default;
            _position = new Vector2(0, 0);
        }

        public ICursor CurrentCursor
        {
            get
            {
                ICursor cursor = GetCursor(_currentCursorType);
                return cursor;
            }
        }

        public bool IsPressed
        {
            get { return _isPressed; }
            set { _isPressed = value; }
        }

        public Vector2 Position
        {
            get { return _position; }
            set { _position = value; }
        }

        public ICursor GetCursor(CursorType type)
        {
            if (_cursorCache.TryGetValue(type, out ICursor cached))
            {
                return cached;
            }

            ICursor cursor = CreateCursor(type);
            if (cursor != null)
            {
                _cursorCache[type] = cursor;
            }

            return cursor;
        }

        public void SetCursor(CursorType type)
        {
            _currentCursorType = type;
        }

        private ICursor CreateCursor(CursorType type)
        {
            // Try to load cursor from game resources first
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Cursors are loaded from EXE PE resources (cursor groups)
            // If Installation is provided and has cursor resources available, load from resources
            // Otherwise, fall back to programmatic creation
            ICursor cursor = TryLoadCursorFromResources(type);
            if (cursor != null)
            {
                return cursor;
            }

            // Fall back to programmatic cursor textures
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): If cursor resources not available, use fallback cursors
            ITexture2D textureUp = CreateCursorTexture(type, false);
            ITexture2D textureDown = CreateCursorTexture(type, true);

            if (textureUp == null || textureDown == null)
            {
                return null;
            }

            // Determine hotspot based on cursor type
            // Default hotspot is top-left (0, 0) for most cursors
            int hotspotX = 0;
            int hotspotY = 0;

            // For hand cursor, hotspot is typically at the tip of the pointing finger
            if (type == CursorType.Hand)
            {
                hotspotX = textureUp.Width / 4;
                hotspotY = textureUp.Height / 4;
            }

            return new MonoGameCursor(textureUp, textureDown, hotspotX, hotspotY);
        }

        /// <summary>
        /// Attempts to load a cursor from game resources.
        /// </summary>
        /// <param name="type">The cursor type to load.</param>
        /// <returns>Loaded cursor if successful, null otherwise.</returns>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Cursor loading from EXE PE resources
        /// - Cursors are stored as Windows PE resources in the EXE file
        /// - Cursor groups contain references to cursor resources (CUR format)
        /// - Each cursor type has two cursor groups (up and down states)
        /// - Cursor group format: 4 bytes reserved, 2 bytes resCount, then cursor entries
        /// - Cursor resource format: CUR file with hotspot and bitmap data
        /// - Note: Full implementation requires PE resource reading from EXE file
        /// - For now, this method provides the structure for future PE resource support
        /// </remarks>
        [CanBeNull]
        private ICursor TryLoadCursorFromResources(CursorType type)
        {
            if (_installation == null)
            {
                // No installation provided - cannot load from resources
                return null;
            }

            // Get cursor group IDs for this cursor type
            if (!CursorGroupIds.TryGetValue(type, out (int UpGroupId, int DownGroupId) groupIds))
            {
                // Unknown cursor type - cannot load from resources
                return null;
            }

            // Try to load cursor group and extract cursor resources
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_00633270 loads cursor groups from EXE PE resources
            // Cursor groups are stored as Windows PE resources (type 0xC = kPEGroupCursor)
            // Cursor resources are stored as Windows PE resources (type 0x1 = kPECursor)

            // Note: Full implementation would require:
            // 1. PE resource reading from EXE file (swkotor.exe or swkotor2.exe)
            // 2. Parsing cursor group structure to get cursor resource IDs
            // 3. Loading cursor resources (CUR format) and extracting bitmap/hotspot data
            // 4. Converting CUR bitmap data to texture format

            // For now, we check if cursor resources might be available in the installation
            // In the future, when PE resource reading is implemented, this method will:
            // - Read cursor group from EXE PE resources using group ID
            // - Parse cursor group to get cursor resource IDs
            // - Load cursor resources (CUR format) from EXE PE resources
            // - Parse CUR format to extract bitmap data and hotspot
            // - Convert bitmap data to ITexture2D
            // - Create MonoGameCursor with textures and hotspot

            // Since PE resource reading is not yet implemented, we return null to fall back to programmatic creation
            // This ensures the cursor manager is ready for future PE resource support
            return null;
        }

        /// <summary>
        /// Parses a cursor group structure to extract cursor resource IDs.
        /// </summary>
        /// <param name="groupData">The cursor group data bytes.</param>
        /// <returns>List of cursor resource IDs, or empty list if parsing fails.</returns>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Cursor group format
        /// - Bytes 0-3: Reserved (4 bytes)
        /// - Bytes 4-5: Resource count (uint16, little-endian)
        /// - For each cursor entry (16 bytes):
        ///   - Bytes 0-1: Width (uint16)
        ///   - Bytes 2-3: Height (uint16, actual height = value / 2)
        ///   - Bytes 4-5: Planes (uint16)
        ///   - Bytes 6-7: BitCount (uint16)
        ///   - Bytes 8-11: BytesInRes (uint32)
        ///   - Bytes 12-13: Cursor ID (uint16) - this is the cursor resource ID
        ///   - Bytes 14-15: Reserved (2 bytes)
        /// Based on xoreos pefile.cpp: Cursor group parsing implementation
        /// </remarks>
        private List<int> ParseCursorGroup(byte[] groupData)
        {
            List<int> cursorIds = new List<int>();

            if (groupData == null || groupData.Length < 6)
            {
                return cursorIds;
            }

            try
            {
                using (MemoryStream stream = new MemoryStream(groupData))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    // Skip reserved bytes (4 bytes)
                    reader.ReadUInt32();

                    // Read resource count
                    ushort resCount = reader.ReadUInt16();
                    if (resCount == 0 || resCount > 100) // Sanity check
                    {
                        return cursorIds;
                    }

                    // Read each cursor entry
                    for (int i = 0; i < resCount; i++)
                    {
                        if (stream.Position + 16 > stream.Length)
                        {
                            break; // Not enough data
                        }

                        // Read cursor entry (16 bytes)
                        ushort width = reader.ReadUInt16();
                        ushort height = reader.ReadUInt16(); // Actual height = height / 2
                        ushort planes = reader.ReadUInt16();
                        ushort bitCount = reader.ReadUInt16();
                        uint bytesInRes = reader.ReadUInt32();
                        ushort cursorId = reader.ReadUInt16();
                        reader.ReadUInt16(); // Reserved (2 bytes)

                        // Add cursor ID to list
                        cursorIds.Add(cursorId);
                    }
                }
            }
            catch
            {
                // Parsing failed - return empty list
                return new List<int>();
            }

            return cursorIds;
        }

        /// <summary>
        /// Parses a CUR (cursor) file to extract texture data and hotspot.
        /// </summary>
        /// <param name="curData">The CUR file data bytes.</param>
        /// <param name="width">Output: Cursor width.</param>
        /// <param name="height">Output: Cursor height.</param>
        /// <param name="hotspotX">Output: Hotspot X coordinate.</param>
        /// <param name="hotspotY">Output: Hotspot Y coordinate.</param>
        /// <param name="pixels">Output: RGBA pixel data.</param>
        /// <returns>True if parsing succeeded, false otherwise.</returns>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): CUR file format parsing
        /// - CUR format is similar to ICO format but includes hotspot information
        /// - Header: 6 bytes (reserved, type, count)
        /// - Directory entry: 16 bytes (width, height, colorCount, reserved, planes, bitCount, size, offset)
        /// - Hotspot: 4 bytes (hotspotX, hotspotY) at offset + size - 4
        /// - Bitmap data: DIB (Device Independent Bitmap) format
        /// - Based on xoreos WinIconImage: CUR file parsing implementation
        /// - Note: Full implementation would require DIB bitmap parsing and conversion to RGBA
        /// </remarks>
        private bool ParseCursorFile(byte[] curData, out int width, out int height, out int hotspotX, out int hotspotY, out byte[] pixels)
        {
            width = 0;
            height = 0;
            hotspotX = 0;
            hotspotY = 0;
            pixels = null;

            if (curData == null || curData.Length < 22)
            {
                return false;
            }

            try
            {
                using (MemoryStream stream = new MemoryStream(curData))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    // Read CUR header (6 bytes)
                    ushort reserved = reader.ReadUInt16(); // Should be 0
                    ushort type = reader.ReadUInt16(); // Should be 2 for CUR
                    ushort count = reader.ReadUInt16(); // Number of images (usually 1)

                    if (type != 2 || count == 0 || count > 10)
                    {
                        return false;
                    }

                    // Read directory entry (16 bytes)
                    byte entryWidth = reader.ReadByte(); // 0 = 256
                    byte entryHeight = reader.ReadByte(); // 0 = 256
                    byte colorCount = reader.ReadByte();
                    byte reserved2 = reader.ReadByte();
                    ushort planes = reader.ReadUInt16();
                    ushort bitCount = reader.ReadUInt16();
                    uint size = reader.ReadUInt32();
                    uint offset = reader.ReadUInt32();

                    width = entryWidth == 0 ? 256 : entryWidth;
                    height = entryHeight == 0 ? 256 : entryHeight;

                    if (offset + size > curData.Length)
                    {
                        return false;
                    }

                    // Read hotspot (last 4 bytes of bitmap data)
                    // Hotspot is stored at offset + size - 4
                    stream.Position = offset + size - 4;
                    hotspotX = reader.ReadUInt16();
                    hotspotY = reader.ReadUInt16();

                    // Read bitmap data (DIB format)
                    // Note: Full implementation would parse DIB and convert to RGBA
                    // For now, we return false to indicate parsing is not fully implemented
                    // This allows the structure to be in place for future DIB parsing support
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        private ITexture2D CreateCursorTexture(CursorType type, bool pressed)
        {
            int width = 32;
            int height = 32;
            byte[] pixels = new byte[width * height * 4]; // RGBA

            // Create cursor texture based on type
            if (type == CursorType.Hand)
            {
                CreateHandCursorPixels(pixels, width, height, pressed);
            }
            else
            {
                // Default cursor (arrow)
                CreateDefaultCursorPixels(pixels, width, height, pressed);
            }

            return _graphicsDevice.CreateTexture2D(width, height, pixels);
        }

        private void CreateDefaultCursorPixels(byte[] pixels, int width, int height, bool pressed)
        {
            // Create a simple arrow cursor
            // Arrow points up-left (typical default cursor)
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * width + x) * 4;
                    byte r = 255;
                    byte g = 255;
                    byte b = 255;
                    byte a = 0;

                    // Draw arrow shape
                    if (x < width / 2 && y < height / 2)
                    {
                        // Arrow shaft (diagonal line)
                        if (x == y || x == y - 1 || x == y + 1)
                        {
                            a = 255;
                        }
                        // Arrow head (triangle)
                        else if (x < width / 4 && y < height / 4)
                        {
                            int dx = x - 0;
                            int dy = y - 0;
                            if (dx + dy < width / 4)
                            {
                                a = 255;
                            }
                        }
                    }

                    // Outline (black)
                    if (a > 0)
                    {
                        bool hasOutline = false;
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int nx = x + dx;
                                int ny = y + dy;
                                if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                                {
                                    int nIndex = (ny * width + nx) * 4;
                                    if (pixels[nIndex + 3] == 0)
                                    {
                                        hasOutline = true;
                                        break;
                                    }
                                }
                            }
                        }
                        if (hasOutline && (x == 0 || y == 0 || x == width - 1 || y == height - 1 ||
                            (x > 0 && pixels[((y * width + (x - 1)) * 4) + 3] == 0) ||
                            (y > 0 && pixels[(((y - 1) * width + x) * 4) + 3] == 0)))
                        {
                            r = 0;
                            g = 0;
                            b = 0;
                        }
                    }

                    pixels[index] = r;
                    pixels[index + 1] = g;
                    pixels[index + 2] = b;
                    pixels[index + 3] = a;
                }
            }
        }

        private void CreateHandCursorPixels(byte[] pixels, int width, int height, bool pressed)
        {
            // Create a simple hand/pointer cursor
            // Hand shape: pointing finger with palm
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * width + x) * 4;
                    byte r = 255;
                    byte g = 255;
                    byte b = 255;
                    byte a = 0;

                    // Hand shape: pointing finger pointing up-right
                    // Finger (top-right area)
                    if (x > width * 0.6f && y < height * 0.4f)
                    {
                        // Pointing finger
                        int fingerX = x - (int)(width * 0.6f);
                        int fingerY = y;
                        if (fingerX < fingerY * 0.5f + 2 && fingerX > fingerY * 0.5f - 2)
                        {
                            a = 255;
                        }
                        // Finger tip
                        if (x > width * 0.75f && y < height * 0.15f)
                        {
                            a = 255;
                        }
                    }

                    // Palm (middle area)
                    if (x > width * 0.3f && x < width * 0.7f && y > height * 0.3f && y < height * 0.7f)
                    {
                        a = 255;
                    }

                    // Thumb (left side)
                    if (x < width * 0.4f && y > height * 0.4f && y < height * 0.7f)
                    {
                        int thumbX = x;
                        int thumbY = y - (int)(height * 0.4f);
                        if (thumbX > thumbY * 0.8f - 2 && thumbX < thumbY * 0.8f + 2)
                        {
                            a = 255;
                        }
                    }

                    // Outline
                    if (a > 0)
                    {
                        // Check for outline pixels
                        bool needsOutline = false;
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int nx = x + dx;
                                int ny = y + dy;
                                if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                                {
                                    int nIndex = (ny * width + nx) * 4;
                                    if (pixels[nIndex + 3] == 0)
                                    {
                                        needsOutline = true;
                                        break;
                                    }
                                }
                            }
                            if (needsOutline)
                            {
                                break;
                            }
                        }
                        if (needsOutline)
                        {
                            r = 0;
                            g = 0;
                            b = 0;
                        }
                    }

                    // Pressed state: slightly darker
                    if (pressed && a > 0)
                    {
                        r = (byte)(r * 0.7f);
                        g = (byte)(g * 0.7f);
                        b = (byte)(b * 0.7f);
                    }

                    pixels[index] = r;
                    pixels[index + 1] = g;
                    pixels[index + 2] = b;
                    pixels[index + 3] = a;
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                // Dispose all cached cursors
                foreach (ICursor cursor in _cursorCache.Values)
                {
                    cursor?.Dispose();
                }
                _cursorCache.Clear();
                _disposed = true;
            }
        }
    }
}


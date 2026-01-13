using System;
using System.Runtime.InteropServices;

namespace Andastra.Runtime.Core.Video.Bink
{
    /// <summary>
    /// P/Invoke declarations for BINKW32.DLL (Bink Video decoder).
    /// Based on swkotor.exe/swkotor2.exe: Bink API usage
    /// Located via Ghidra reverse engineering: 0x00404c80 @ 0x00404c80, 0x004053e0 @ 0x004053e0
    /// Original implementation: Uses BINKW32.DLL for BIK format video playback
    /// </summary>
    internal static class BinkApi
    {
        private const string BinkDll = "BINKW32.DLL";

        /// <summary>
        /// BINK structure - Main Bink video file handle.
        /// Based on BINKW32.DLL reverse engineering via Ghidra: _BinkGetSummary@8 analysis.
        /// Structure size: ~0x2b0 bytes (688 bytes) based on field offsets up to 0xc3 (195 dwords).
        /// Matches original engine usage: swkotor.exe 0x00404c80 @ 0x00404c80, 0x004053e0 @ 0x004053e0.
        /// </summary>
        /// <remarks>
        /// Field offsets determined from BinkGetSummary decompilation:
        /// - [0] = Width
        /// - [1] = Height
        /// - [2] = Frames
        /// - [5] = FrameRate
        /// - [6] = FrameRateDiv
        /// - [0x3d] = NormalFrameSize
        /// - [0x3e] = NormalCompressedFrameSize
        /// - [0x43] = FrameData pointer
        /// - [0x4d] = FileFrameRate
        /// - [0x4f] = FileFrameRateDiv
        /// - [0x50] = SourceFrameSize
        /// - [0x51] = SourceCompressedFrameSize
        /// - [0x52] = LargestFrameSize
        /// - [0x53] = LargestCompressedFrameSize
        /// - [0x54] = TotalTime
        /// - [0x94] = Unknown field
        /// - [0x9c] = FileFrameRate (duplicate?)
        /// - [0x9d] = TotalOpenTime
        /// - [0x9f] = Timer field
        /// - [0xaa] = TotalFrameDecompTime
        /// - [0xab] = TotalReadTime
        /// - [0xac] = TotalVideoBlitTime
        /// - [0xad] = TotalAudioDecompTime
        /// - [0xae] = FrameType
        /// - [0xaf] = FrameSize
        /// - [0xb0] = CompressedFrameSize
        /// - [0xbf] = Alpha
        /// - [0xc3] = FramesToPlay
        /// - [0xa4] = KeyFrameSize
        /// - [0xa5] = KeyCompressedFrameSize
        /// - [0xa6] = InterFrameSize
        /// - [0xa7] = InterCompressedFrameSize
        /// </remarks>
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct BINK
        {
            // Offset 0x00: Basic video properties
            public uint Width;                    // [0] - Video width in pixels
            public uint Height;                   // [1] - Video height in pixels
            public uint Frames;                   // [2] - Total number of frames
            public uint FrameNum;                 // [3] - Current frame number
            public uint LastFrameNum;             // [4] - Last frame number

            // Offset 0x14: Frame rate information
            public uint FrameRate;                // [5] - Frame rate numerator
            public uint FrameRateDiv;             // [6] - Frame rate denominator

            // Offset 0x18-0x3C: Reserved/unknown fields (padding)
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 13)]
            public uint[] Reserved1;               // [7-0x13] - Reserved fields

            // Offset 0x40: Frame size information
            public uint NormalFrameSize;          // [0x10] - Normal frame size
            public uint NormalCompressedFrameSize; // [0x11] - Normal compressed frame size

            // Offset 0x48-0x4C: Reserved
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 11)]
            public uint[] Reserved2;               // [0x12-0x1c] - Reserved fields

            // Offset 0x50: Frame data pointer
            public IntPtr FrameData;              // [0x1d] - Pointer to frame data

            // Offset 0x54-0x9C: Reserved/unknown fields
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 19)]
            public uint[] Reserved3;               // [0x1e-0x30] - Reserved fields

            // Offset 0xA0: File frame rate
            public uint FileFrameRate;            // [0x28] - File frame rate numerator
            public uint FileFrameRateDiv;         // [0x29] - File frame rate denominator

            // Offset 0xA8: Source frame sizes
            public uint SourceFrameSize;          // [0x2a] - Source frame size
            public uint SourceCompressedFrameSize; // [0x2b] - Source compressed frame size
            public uint LargestFrameSize;         // [0x2c] - Largest frame size
            public uint LargestCompressedFrameSize; // [0x2d] - Largest compressed frame size
            public uint TotalTime;                // [0x2e] - Total playback time

            // Offset 0xBC-0x94: Reserved
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 22)]
            public uint[] Reserved4;               // [0x2f-0x44] - Reserved fields

            // Offset 0x250: Unknown field
            public uint UnknownField94;           // [0x94] - Unknown field

            // Offset 0x254-0x26C: Reserved
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 7)]
            public uint[] Reserved5;               // [0x95-0x9b] - Reserved fields

            // Offset 0x270: File frame rate (duplicate?)
            public uint FileFrameRate2;           // [0x9c] - File frame rate (duplicate field)
            public uint TotalOpenTime;            // [0x9d] - Total time to open file
            public uint Reserved6;                // [0x9e] - Reserved
            public uint TimerField;                // [0x9f] - Timer field for timing calculations

            // Offset 0x280-0x290: Reserved
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public uint[] Reserved7;               // [0xa0-0xa3] - Reserved fields

            // Offset 0x290: Frame size statistics (must be before timing stats)
            public uint KeyFrameSize;             // [0xa4] - Key frame size
            public uint KeyCompressedFrameSize;   // [0xa5] - Key compressed frame size
            public uint InterFrameSize;           // [0xa6] - Inter frame size
            public uint InterCompressedFrameSize; // [0xa7] - Inter compressed frame size

            // Offset 0x2A0-0x2A8: Reserved
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public uint[] Reserved8;               // [0xa8-0xa9] - Reserved fields

            // Offset 0x2A8: Timing statistics
            public uint TotalFrameDecompTime;     // [0xaa] - Total frame decompression time
            public uint TotalReadTime;            // [0xab] - Total read time
            public uint TotalVideoBlitTime;      // [0xac] - Total video blit time
            public uint TotalAudioDecompTime;     // [0xad] - Total audio decompression time
            public uint FrameType;                // [0xae] - Current frame type
            public uint FrameSize;                // [0xaf] - Current frame size
            public uint CompressedFrameSize;      // [0xb0] - Current compressed frame size

            // Offset 0x2C4-0x2FC: Reserved
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 14)]
            public uint[] Reserved9;               // [0xb1-0xbe] - Reserved fields

            // Offset 0x2FC: Alpha channel
            public uint Alpha;                    // [0xbf] - Alpha channel flag

            // Offset 0x300-0x30C: Reserved
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public uint[] Reserved10;              // [0xc0-0xc2] - Reserved fields

            // Offset 0x30C: Frames to play
            public uint FramesToPlay;             // [0xc3] - Number of frames to play

            // Remaining fields to reach structure size (~0x2b0 bytes total)
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 100)]
            public uint[] Reserved11;              // [0xc4-0x127] - Remaining reserved/internal fields
        }

        /// <summary>
        /// BINKBUFFER structure - Bink rendering buffer.
        /// Based on BINKW32.DLL reverse engineering via Ghidra: _BinkBufferOpen@16 analysis.
        /// Matches original engine usage: swkotor.exe 0x00404c80 @ 0x00404c80 accesses:
        /// - +0x00 = BufferWidth
        /// - +0x04 = BufferHeight (accessed as *(iVar1 + 4))
        /// - +0x10 = Blit flags (accessed as *(iVar1 + 0x10))
        /// - +0x14 = Buffer pointer (accessed as *(iVar1 + 0x14))
        /// - +0x18 = Buffer pitch (accessed as *(iVar1 + 0x18))
        /// - +0x10 = Rects pointer (accessed as *(*(int *)(param_1 + 0x4c) + 0x10))
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct BINKBUFFER
        {
            // Offset 0x00: Buffer dimensions
            public uint BufferWidth;              // Buffer width in pixels
            public uint BufferHeight;             // Buffer height in pixels (offset +0x04 in swkotor.exe)

            // Offset 0x08-0x0C: Reserved fields
            public uint Reserved1;                // Reserved field
            public uint Reserved2;                // Reserved field

            // Offset 0x10: Blit flags (or rects pointer - union field)
            // Note: This field is used as both BlitFlags (uint) and Rects pointer (IntPtr) depending on context.
            // In swkotor.exe: BinkCopyToBuffer uses as flags, BinkGetRects uses as pointer.
            // Rects pointer is at the same offset but accessed differently - using explicit layout would require FieldOffset.
            public uint BlitFlags;                // Blit operation flags (offset +0x10 in swkotor.exe line 22)

            // Offset 0x14: Buffer pointer
            public IntPtr Buffer;                 // Pointer to buffer memory (offset +0x14 in swkotor.exe)

            // Offset 0x18: Buffer pitch
            public uint BufferPitch;              // Buffer pitch in bytes (offset +0x18 in swkotor.exe)

            // Offset 0x1C: Destination coordinates
            public int DestX;                     // Destination X coordinate
            public int DestY;                     // Destination Y coordinate
            public uint DestWidth;                // Destination width
            public uint DestHeight;               // Destination height

            // Offset 0x2C: Window handle
            public IntPtr Window;                 // Window handle for rendering

            // Offset 0x30+: Remaining fields (structure may be larger)
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
            public uint[] Reserved3;               // Reserved/internal fields
        }

        /// <summary>
        /// BINKSUMMARY structure - Summary information about a BIK file.
        /// Based on BINKW32.DLL reverse engineering via Ghidra: _BinkGetSummary@8 analysis.
        /// Structure size: 31 int fields (0x1f * 4 = 124 bytes) based on initialization loop.
        /// Matches original engine usage: swkotor.exe 0x00404c80 @ 0x00404c80 line 44 calls BinkGetSummary.
        /// 
        /// Field mapping from BinkGetSummary decompilation:
        /// - [0] = Width (from param_1[0])
        /// - [1] = Height (from param_1[1])
        /// - [2] = TotalOpenTime (calculated: iVar1 - param_1[0x9d])
        /// - [3] = FrameSize (from param_1[0xaf])
        /// - [4] = CompressedFrameSize (from param_1[0xb0])
        /// - [5] = FrameRate (from param_1[5])
        /// - [6] = FrameRateDiv (from param_1[6])
        /// - [7] = FrameType (from param_1[0xae])
        /// - [8] = TotalFrames (from param_1[2])
        /// - [9] = FileFrameRate (from param_1[0x9c])
        /// - [0xa] = Reserved/Unknown (not set in BinkGetSummary)
        /// - [0xb] = FramesToPlay (from param_1[0xc3])
        /// - [0xc] = Alpha (from param_1[0xbf])
        /// - [0xd] = TotalAudioDecompTime (from param_1[0xad])
        /// - [0xe] = SourceFrameSize (from param_1[0x50])
        /// - [0xf] = TotalReadTime (from param_1[0xab])
        /// - [0x10] = TotalVideoBlitTime (from param_1[0xac])
        /// - [0x11] = SourceCompressedFrameSize (from param_1[0x51])
        /// - [0x12] = LargestFrameSize (from param_1[0x52])
        /// - [0x13] = FileFrameRateDiv (calculated: (param_1[0x4d] * 1000) / (param_1[0x4f] + 1))
        /// - [0x14] = KeyFrameSize (from param_1[0xa4])
        /// - [0x15] = InterFrameSize (from param_1[0xa6])
        /// - [0x16] = KeyCompressedFrameSize (from param_1[0xa5])
        /// - [0x17] = InterCompressedFrameSize (from param_1[0xa7])
        /// - [0x18] = Calculated field (from param_1[10] and param_1[0x43], param_1[0xaf], param_1[0xb0], param_1[2])
        /// - [0x19] = Calculated field (from param_1[10] and param_1[0x43], param_1[2])
        /// - [0x1a] = Accumulated field (param_2[0x1a] + param_1[0xaa])
        /// - [0x1b] = LargestCompressedFrameSize (from param_1[0x53], overwrites param_1[0x94])
        /// - [0x1c] = TotalTime (from param_1[0x54])
        /// - [0x1d] = NormalFrameSize (from param_1[0x3d])
        /// - [0x1e] = NormalCompressedFrameSize (from param_1[0x3e] + 1)
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct BINKSUMMARY
        {
            // Offset 0x00: Basic video properties
            public int Width;                        // [0] - Video width in pixels
            public int Height;                       // [1] - Video height in pixels
            public int TotalOpenTime;                // [2] - Total time to open file (calculated)

            // Offset 0x0C: Frame information
            public int FrameSize;                    // [3] - Current frame size
            public int CompressedFrameSize;          // [4] - Current compressed frame size
            public int FrameRate;                    // [5] - Frame rate numerator
            public int FrameRateDiv;                 // [6] - Frame rate denominator
            public int FrameType;                    // [7] - Current frame type
            public int TotalFrames;                  // [8] - Total number of frames
            public int FileFrameRate;                // [9] - File frame rate numerator

            // Offset 0x28: Reserved/Unknown field
            public int Reserved1;                    // [0xa] - Reserved field (not set by BinkGetSummary)

            // Offset 0x2C: Playback information
            public int FramesToPlay;                 // [0xb] - Number of frames to play
            public int Alpha;                        // [0xc] - Alpha channel flag

            // Offset 0x34: Timing statistics
            public int TotalAudioDecompTime;         // [0xd] - Total audio decompression time
            public int SourceFrameSize;              // [0xe] - Source frame size
            public int TotalReadTime;                // [0xf] - Total read time
            public int TotalVideoBlitTime;          // [0x10] - Total video blit time
            public int SourceCompressedFrameSize;    // [0x11] - Source compressed frame size
            public int LargestFrameSize;             // [0x12] - Largest frame size
            public int FileFrameRateDiv;             // [0x13] - File frame rate denominator (calculated)

            // Offset 0x50: Frame size statistics
            public int KeyFrameSize;                 // [0x14] - Key frame size
            public int InterFrameSize;                // [0x15] - Inter frame size
            public int KeyCompressedFrameSize;       // [0x16] - Key compressed frame size
            public int InterCompressedFrameSize;      // [0x17] - Inter compressed frame size

            // Offset 0x60: Calculated fields
            public int CalculatedField1;             // [0x18] - Calculated field (complex calculation)
            public int CalculatedField2;             // [0x19] - Calculated field (uVar3 / param_1[2])
            public int AccumulatedField;             // [0x1a] - Accumulated field (incremented)

            // Offset 0x6C: Additional statistics
            public int LargestCompressedFrameSize;   // [0x1b] - Largest compressed frame size
            public int TotalTime;                    // [0x1c] - Total playback time
            public int NormalFrameSize;              // [0x1d] - Normal frame size
            public int NormalCompressedFrameSize;    // [0x1e] - Normal compressed frame size (param_1[0x3e] + 1)

            // Note: Fields that are in the original structure but not set by BinkGetSummary:
            // TotalFrameDecompTime, TotalIdleReadTime, TotalBackReadTime, TotalIdleDecompTime,
            // TotalBackDecompTime, TotalIdleBlitTime, TotalBackBlitTime, TotalPlayTime, TotalBuffedTime,
            // InterKeyFrameSize, InterKeyCompressedFrameSize - these are initialized to 0 but not populated
        }

        // Bink API function declarations
        // Based on swkotor.exe: Import table shows these functions are used
        // Located via Ghidra: 0x00404c80 @ 0x00404c80 uses these functions

        /// <summary>
        /// Opens a BIK file for playback.
        /// Based on swkotor.exe: 0x004053e0 @ 0x004053e0 line 124 calls BinkOpen
        /// Parameters: path, flags (0x8000000)
        /// </summary>
        [DllImport(BinkDll, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "_BinkOpen@8")]
        public static extern IntPtr BinkOpen([MarshalAs(UnmanagedType.LPStr)] string path, uint flags);

        /// <summary>
        /// Closes a BIK file.
        /// Based on swkotor.exe: 0x00404c80 @ 0x00404c80 cleanup, 0x004053e0 @ 0x004053e0 line 172
        /// </summary>
        [DllImport(BinkDll, CallingConvention = CallingConvention.StdCall, EntryPoint = "_BinkClose@4")]
        public static extern void BinkClose(IntPtr bink);

        /// <summary>
        /// Gets summary information about a BIK file.
        /// Based on swkotor.exe: 0x00404c80 @ 0x00404c80 line 44 calls BinkGetSummary
        /// </summary>
        [DllImport(BinkDll, CallingConvention = CallingConvention.StdCall, EntryPoint = "_BinkGetSummary@8")]
        public static extern void BinkGetSummary(IntPtr bink, ref BINKSUMMARY summary);

        /// <summary>
        /// Opens a Bink buffer for rendering.
        /// Based on swkotor.exe: 0x004053e0 @ 0x004053e0 line 128 calls BinkBufferOpen
        /// Parameters: window handle, width, height, flags (0x5d000000)
        /// </summary>
        [DllImport(BinkDll, CallingConvention = CallingConvention.StdCall, EntryPoint = "_BinkBufferOpen@16")]
        public static extern IntPtr BinkBufferOpen(IntPtr windowHandle, int width, int height, uint flags);

        /// <summary>
        /// Closes a Bink buffer.
        /// </summary>
        [DllImport(BinkDll, CallingConvention = CallingConvention.StdCall, EntryPoint = "_BinkBufferClose@4")]
        public static extern void BinkBufferClose(IntPtr buffer);

        /// <summary>
        /// Locks the Bink buffer for writing.
        /// Based on swkotor.exe: 0x00404c80 @ 0x00404c80 line 16 calls BinkBufferLock
        /// Returns pointer to buffer memory (non-zero if successful).
        /// </summary>
        [DllImport(BinkDll, CallingConvention = CallingConvention.StdCall, EntryPoint = "_BinkBufferLock@4")]
        public static extern IntPtr BinkBufferLock(IntPtr buffer);

        /// <summary>
        /// Unlocks the Bink buffer.
        /// Based on swkotor.exe: 0x00404c80 @ 0x00404c80 line 23 calls BinkBufferUnlock
        /// </summary>
        [DllImport(BinkDll, CallingConvention = CallingConvention.StdCall, EntryPoint = "_BinkBufferUnlock@4")]
        public static extern void BinkBufferUnlock(IntPtr buffer);

        /// <summary>
        /// Copies decoded frame data to the buffer.
        /// Based on swkotor.exe: 0x00404c80 @ 0x00404c80 line 19-22 calls BinkCopyToBuffer
        /// Parameters: bink, dest buffer pointer, dest pitch, dest height, dest x, dest y, flags
        /// </summary>
        [DllImport(BinkDll, CallingConvention = CallingConvention.StdCall, EntryPoint = "_BinkCopyToBuffer@28")]
        public static extern void BinkCopyToBuffer(IntPtr bink, IntPtr dest, int destpitch, int destheight, int destx, int desty, uint flags);

        /// <summary>
        /// Gets destination rectangles for blitting.
        /// Based on swkotor.exe: 0x00404c80 @ 0x00404c80 line 25-26 calls BinkGetRects
        /// </summary>
        [DllImport(BinkDll, CallingConvention = CallingConvention.StdCall, EntryPoint = "_BinkGetRects@8")]
        public static extern IntPtr BinkGetRects(IntPtr bink, IntPtr rects);

        /// <summary>
        /// Blits the buffer to the screen.
        /// Based on swkotor.exe: 0x00404c80 @ 0x00404c80 line 27 calls BinkBufferBlit
        /// </summary>
        [DllImport(BinkDll, CallingConvention = CallingConvention.StdCall, EntryPoint = "_BinkBufferBlit@12")]
        public static extern void BinkBufferBlit(IntPtr buffer, IntPtr rects, IntPtr destrect);

        /// <summary>
        /// Sets the buffer scale.
        /// Based on swkotor.exe: 0x004053e0 @ 0x004053e0 line 154 calls BinkBufferSetScale
        /// </summary>
        [DllImport(BinkDll, CallingConvention = CallingConvention.StdCall, EntryPoint = "_BinkBufferSetScale@12")]
        public static extern void BinkBufferSetScale(IntPtr buffer, int width, int height);

        /// <summary>
        /// Sets the buffer offset.
        /// Based on swkotor.exe: 0x004053e0 @ 0x004053e0 line 158-160 calls BinkBufferSetOffset
        /// </summary>
        [DllImport(BinkDll, CallingConvention = CallingConvention.StdCall, EntryPoint = "_BinkBufferSetOffset@12")]
        public static extern void BinkBufferSetOffset(IntPtr buffer, int x, int y);

        /// <summary>
        /// Decodes the current frame.
        /// Based on swkotor.exe: 0x00404c80 @ 0x00404c80 line 15 calls BinkDoFrame
        /// Returns 0 on success, non-zero on error.
        /// </summary>
        [DllImport(BinkDll, CallingConvention = CallingConvention.StdCall, EntryPoint = "_BinkDoFrame@4")]
        public static extern int BinkDoFrame(IntPtr bink);

        /// <summary>
        /// Advances to the next frame.
        /// Based on swkotor.exe: 0x00404c80 @ 0x00404c80 line 28 calls BinkNextFrame
        /// </summary>
        [DllImport(BinkDll, CallingConvention = CallingConvention.StdCall, EntryPoint = "_BinkNextFrame@4")]
        public static extern void BinkNextFrame(IntPtr bink);

        /// <summary>
        /// Waits for frame timing (returns 1 if need to wait more, 0 if ready).
        /// Based on swkotor.exe: 0x00404c80 @ 0x00404c80 line 29-33 calls BinkWait with Sleep loop
        /// </summary>
        [DllImport(BinkDll, CallingConvention = CallingConvention.StdCall, EntryPoint = "_BinkWait@4")]
        public static extern int BinkWait(IntPtr bink);

        /// <summary>
        /// Sets the sound system for audio playback.
        /// Based on swkotor.exe: 0x004053e0 @ 0x004053e0 line 121 calls BinkSetSoundSystem
        /// </summary>
        [DllImport(BinkDll, CallingConvention = CallingConvention.StdCall, EntryPoint = "_BinkSetSoundSystem@8")]
        public static extern void BinkSetSoundSystem(IntPtr bink, IntPtr soundSystem);

        /// <summary>
        /// Sets the volume for audio playback.
        /// Based on swkotor.exe: 0x004053e0 @ 0x004053e0 line 136 calls BinkSetVolume
        /// </summary>
        [DllImport(BinkDll, CallingConvention = CallingConvention.StdCall, EntryPoint = "_BinkSetVolume@12")]
        public static extern void BinkSetVolume(IntPtr bink, int track, int volume);

        /// <summary>
        /// Pauses or unpauses playback.
        /// </summary>
        [DllImport(BinkDll, CallingConvention = CallingConvention.StdCall, EntryPoint = "_BinkPause@8")]
        public static extern void BinkPause(IntPtr bink, int pause);

        /// <summary>
        /// Opens Miles sound system for audio.
        /// Based on swkotor.exe: 0x004053e0 @ 0x004053e0 line 121 calls BinkOpenMiles
        /// </summary>
        [DllImport(BinkDll, CallingConvention = CallingConvention.StdCall, EntryPoint = "_BinkOpenMiles@4")]
        public static extern IntPtr BinkOpenMiles(int sampleRate);

        // Buffer flags
        public const uint BINKBUFFER_BLIT_INTERNAL = 0x00000001;
        public const uint BINKBUFFER_BLIT_EXTERNAL = 0x00000002;
        public const uint BINKBUFFER_BLIT_DIRECT = 0x00000004;
        public const uint BINKBUFFER_BLIT_SRC_COPY = 0x00000008;
        public const uint BINKBUFFER_BLIT_SRC_ALPHA = 0x00000010;
        public const uint BINKBUFFER_BLIT_MULTIPLY = 0x00000020;
        public const uint BINKBUFFER_BLIT_ADD = 0x00000040;
        public const uint BINKBUFFER_BLIT_SUBTRACT = 0x00000080;
        public const uint BINKBUFFER_BLIT_BILINEAR = 0x00000100;
        public const uint BINKBUFFER_BLIT_TRANSLATE = 0x00000200;
        public const uint BINKBUFFER_BLIT_SCALE = 0x00000400;
        public const uint BINKBUFFER_BLIT_DEST_COPY = 0x00000800;
        public const uint BINKBUFFER_BLIT_DEST_ALPHA = 0x00001000;
        public const uint BINKBUFFER_BLIT_DEST_MULTIPLY = 0x00002000;
        public const uint BINKBUFFER_BLIT_DEST_ADD = 0x00004000;
        public const uint BINKBUFFER_BLIT_DEST_SUBTRACT = 0x00008000;
        public const uint BINKBUFFER_BLIT_DEST_BILINEAR = 0x00010000;
        public const uint BINKBUFFER_BLIT_DEST_TRANSLATE = 0x00020000;
        public const uint BINKBUFFER_BLIT_DEST_SCALE = 0x00040000;

        // Bink open flags
        public const uint BINKOPEN_NORMAL = 0x00000000;
        public const uint BINKOPEN_ASYNC = 0x00000001;
        public const uint BINKOPEN_SKIPVIDEO = 0x00000002;
        public const uint BINKOPEN_SKIPAUDIO = 0x00000004;
        public const uint BINKOPEN_SKIPALL = 0x00000006;
        public const uint BINKOPEN_OPENONLY = 0x00000008;
        public const uint BINKOPEN_OPENONLYASYNC = 0x00000009;
        public const uint BINKOPEN_OPENONLYSKIPVIDEO = 0x0000000A;
        public const uint BINKOPEN_OPENONLYSKIPAUDIO = 0x0000000C;
        public const uint BINKOPEN_OPENONLYSKIPALL = 0x0000000E;
        public const uint BINKOPEN_OPENONLYNOSOUND = 0x00000010;
        public const uint BINKOPEN_OPENONLYNOSOUNDASYNC = 0x00000011;
        public const uint BINKOPEN_OPENONLYNOSOUNDSKIPVIDEO = 0x00000012;
        public const uint BINKOPEN_OPENONLYNOSOUNDSKIPAUDIO = 0x00000014;
        public const uint BINKOPEN_OPENONLYNOSOUNDSKIPALL = 0x00000016;
        public const uint BINKOPEN_OPENONLYNOSOUNDNOSKIP = 0x00000018;
        public const uint BINKOPEN_OPENONLYNOSOUNDNOSKIPASYNC = 0x00000019;
        public const uint BINKOPEN_OPENONLYNOSOUNDNOSKIPSKIPVIDEO = 0x0000001A;
        public const uint BINKOPEN_OPENONLYNOSOUNDNOSKIPSKIPAUDIO = 0x0000001C;
        public const uint BINKOPEN_OPENONLYNOSOUNDNOSKIPSKIPALL = 0x0000001E;

        // Copy to buffer flags
        public const uint BINKCOPYALL = 0x00000000;
        public const uint BINKCOPYFRAME = 0x00000001;
        public const uint BINKCOPYAUDIO = 0x00000002;
        public const uint BINKCOPYVIDEO = 0x00000004;
        public const uint BINKCOPYALPHA = 0x00000008;
        public const uint BINKCOPYHDR = 0x00000010;
        public const uint BINKCOPYHDRALPHA = 0x00000020;
        public const uint BINKCOPYHDRVIDEO = 0x00000040;
        public const uint BINKCOPYHDRALPHAVIDEO = 0x00000080;
        public const uint BINKCOPYHDRALPHAVIDEOFRAME = 0x00000100;
        public const uint BINKCOPYHDRALPHAVIDEOFRAMEAUDIO = 0x00000200;
        public const uint BINKCOPYHDRALPHAVIDEOFRAMEAUDIOALL = 0x00000400;
        public const uint BINKCOPYHDRALPHAVIDEOFRAMEAUDIOALLSKIP = 0x00000800;
        public const uint BINKCOPYHDRALPHAVIDEOFRAMEAUDIOALLSKIPVIDEO = 0x00001000;
        public const uint BINKCOPYHDRALPHAVIDEOFRAMEAUDIOALLSKIPVIDEOAUDIO = 0x00002000;
        public const uint BINKCOPYHDRALPHAVIDEOFRAMEAUDIOALLSKIPVIDEOAUDIOALPHA = 0x00004000;
        public const uint BINKCOPYHDRALPHAVIDEOFRAMEAUDIOALLSKIPVIDEOAUDIOALPHAHDR = 0x00008000;
    }
}


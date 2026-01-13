using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Core.Video.Bink
{
    /// <summary>
    /// BIK video decoder using BINKW32.DLL.
    /// Based on swkotor.exe: 0x00404c80 @ 0x00404c80 (main playback loop)
    /// </summary>
    internal class BikDecoder : IDisposable
    {
        private IntPtr _binkHandle;
        private IntPtr _bufferHandle;
        private BinkApi.BINKSUMMARY _summary;
        private bool _isDisposed;
        private readonly string _moviePath;
        private readonly IMovieGraphicsDevice _graphicsDevice;
        private IMovieTexture2D _frameTexture;
        private int _frameWidth;
        private int _frameHeight;
        private byte[] _frameBuffer;

        /// <summary>
        /// Initializes a new instance of the BikDecoder class.
        /// </summary>
        /// <param name="moviePath">Path to BIK file.</param>
        /// <param name="graphicsDevice">Graphics device for rendering.</param>
        public BikDecoder(string moviePath, IMovieGraphicsDevice graphicsDevice)
        {
            _moviePath = moviePath ?? throw new ArgumentNullException("moviePath");
            _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException("graphicsDevice");
            _binkHandle = IntPtr.Zero;
            _bufferHandle = IntPtr.Zero;
            _isDisposed = false;
        }

        /// <summary>
        /// Opens the BIK file and initializes playback.
        /// Based on swkotor.exe: 0x004053e0 @ 0x004053e0 (movie initialization)
        /// </summary>
        public void Open()
        {
            if (_binkHandle != IntPtr.Zero)
            {
                throw new InvalidOperationException("BikDecoder is already open");
            }

            // Initialize Miles sound system for audio
            // Based on swkotor.exe: 0x004053e0 @ 0x004053e0 line 121 calls BinkOpenMiles
            // Line 121: _BinkOpenMiles_4_exref = _BinkOpenMiles_4(...)
            IntPtr milesHandle = BinkApi.BinkOpenMiles(22050); // Standard sample rate (can be adjusted)

            // Open BIK file
            // Based on swkotor.exe: 0x004053e0 @ 0x004053e0 line 124 calls BinkOpen
            // Parameters: path, flags (0x8000000)
            const uint BinkOpenFlags = 0x8000000; // Flags from decompilation
            _binkHandle = BinkApi.BinkOpen(_moviePath, BinkOpenFlags);
            if (_binkHandle == IntPtr.Zero)
            {
                throw new IOException(string.Format("Failed to open BIK file: {0}", _moviePath));
            }

            // Set sound system for audio playback
            // Based on swkotor.exe: 0x004053e0 @ 0x004053e0 line 121 calls BinkSetSoundSystem
            if (milesHandle != IntPtr.Zero)
            {
                BinkApi.BinkSetSoundSystem(_binkHandle, milesHandle);
            }

            // Get movie summary
            _summary = new BinkApi.BINKSUMMARY();
            BinkApi.BinkGetSummary(_binkHandle, ref _summary);

            _frameWidth = _summary.Width;
            _frameHeight = _summary.Height;

            // Create buffer for video frames
            // Based on swkotor.exe: 0x004053e0 @ 0x004053e0 line 128 calls BinkBufferOpen
            // Parameters: window handle, width, height, flags (0x5d000000)
            // Line 128: _BinkBufferOpen_16(*(undefined4 *)((int)this + 0x50), *puVar8, puVar8[1], 0x5d000000)
            // windowHandle = this + 0x50 (window handle), width = *puVar8 (BINK width), height = puVar8[1] (BINK height)
            // Get window handle if available
            IntPtr windowHandle = _graphicsDevice.NativeHandle; // Get window handle if available (0 if not available)
            const uint BinkBufferFlags = 0x5d000000; // Flags from decompilation
            _bufferHandle = BinkApi.BinkBufferOpen(windowHandle, _frameWidth, _frameHeight, BinkBufferFlags);
            if (_bufferHandle == IntPtr.Zero)
            {
                BinkApi.BinkClose(_binkHandle);
                _binkHandle = IntPtr.Zero;
                throw new InvalidOperationException("Failed to create Bink buffer");
            }

            // Set buffer scale and offset for fullscreen rendering
            // Based on swkotor.exe: 0x004053e0 @ 0x004053e0 line 154-160
            // BinkBufferSetScale and BinkBufferSetOffset are called to position video
            MovieViewport viewport = _graphicsDevice.Viewport;
            int screenWidth = viewport.Width;
            int screenHeight = viewport.Height;

            // Calculate scaling to fill screen while maintaining aspect ratio
            float scaleX = (float)screenWidth / _frameWidth;
            float scaleY = (float)screenHeight / _frameHeight;
            float scale = Math.Min(scaleX, scaleY);
            int scaledWidth = (int)(_frameWidth * scale);
            int scaledHeight = (int)(_frameHeight * scale);
            int offsetX = (screenWidth - scaledWidth) / 2;
            int offsetY = (screenHeight - scaledHeight) / 2;

            // Set buffer scale for fullscreen rendering
            // Based on swkotor.exe: 0x004053e0 @ 0x004053e0 line 154 calls BinkBufferSetScale
            BinkApi.BinkBufferSetScale(_bufferHandle, scaledWidth, scaledHeight);

            // Set buffer offset for centering
            // Based on swkotor.exe: 0x004053e0 @ 0x004053e0 line 158-160 calls BinkBufferSetOffset
            BinkApi.BinkBufferSetOffset(_bufferHandle, offsetX, offsetY);

            // Allocate frame buffer for copying to texture (RGBA format)
            _frameBuffer = new byte[_frameWidth * _frameHeight * 4];

            // Create texture for rendering
            _frameTexture = _graphicsDevice.CreateTexture2D(_frameWidth, _frameHeight, null);
        }

        /// <summary>
        /// Gets the width of the video.
        /// </summary>
        public int Width
        {
            get { return _frameWidth; }
        }

        /// <summary>
        /// Gets the height of the video.
        /// </summary>
        public int Height
        {
            get { return _frameHeight; }
        }

        /// <summary>
        /// Gets the total number of frames.
        /// </summary>
        public int TotalFrames
        {
            get { return _summary.TotalFrames; }
        }

        /// <summary>
        /// Gets the current frame number.
        /// Based on swkotor.exe: 0x00404c80 @ 0x00404c80 line 13 checks frame count
        /// </summary>
        public int CurrentFrame
        {
            get
            {
                if (_binkHandle == IntPtr.Zero)
                {
                    return 0;
                }
                // Read frame number from BINK structure
                // Based on decompilation: FrameNum is at offset 0x0C in BINK structure
                // Line 13: *(int *)(iVar1 + 8) != *(int *)(iVar1 + 0xc)
                // This compares FrameNum (offset 8) with LastFrameNum (offset 0xC)
                // We'll read FrameNum from offset 8
                try
                {
                    int frameNum = Marshal.ReadInt32(_binkHandle, 8);
                    return frameNum;
                }
                catch
                {
                    return 0;
                }
            }
        }

        /// <summary>
        /// Gets whether playback is complete.
        /// Based on swkotor.exe: 0x00404c80 @ 0x00404c80 line 13 checks frame count
        /// </summary>
        public bool IsComplete
        {
            get
            {
                if (_binkHandle == IntPtr.Zero)
                {
                    return true;
                }
                // Check if current frame >= total frames
                // Based on decompilation: *(int *)(iVar1 + 8) != *(int *)(iVar1 + 0xc)
                // This checks if FrameNum (offset 8) != LastFrameNum (offset 0xC)
                // Movie is complete when FrameNum == LastFrameNum
                try
                {
                    int frameNum = Marshal.ReadInt32(_binkHandle, 8);
                    int lastFrameNum = Marshal.ReadInt32(_binkHandle, 12);
                    return frameNum >= lastFrameNum;
                }
                catch
                {
                    return true; // Assume complete on error
                }
            }
        }

        /// <summary>
        /// Decodes and renders the current frame.
        /// Based on swkotor.exe: 0x00404c80 @ 0x00404c80 (main playback loop)
        /// </summary>
        /// <returns>True if frame was decoded, false if playback is complete.</returns>
        public bool DecodeFrame()
        {
            if (_binkHandle == IntPtr.Zero || IsComplete)
            {
                return false;
            }

            // Decode current frame
            // Based on swkotor.exe: 0x00404c80 @ 0x00404c80 line 15 calls BinkDoFrame
            // BinkDoFrame returns 0 on success, non-zero on error
            int result = BinkApi.BinkDoFrame(_binkHandle);
            if (result != 0)
            {
                // Error decoding frame - check if movie is complete
                // If error is due to end of movie, that's normal
                return false;
            }

            // Lock buffer for writing
            // Based on swkotor.exe: 0x00404c80 @ 0x00404c80 line 16 calls BinkBufferLock
            // BinkBufferLock returns pointer to buffer memory (non-zero if successful)
            // Line 16: iVar1 = _BinkBufferLock_4(*(undefined4 *)(param_1 + 0x4c));
            // Returns buffer pointer directly
            IntPtr bufferPtr = BinkApi.BinkBufferLock(_bufferHandle);
            if (bufferPtr != IntPtr.Zero)
            {
                // Copy decoded frame to buffer
                // Based on swkotor.exe: 0x00404c80 @ 0x00404c80 line 19-22 calls BinkCopyToBuffer
                // Parameters from decompilation:
                //   bink = *(undefined4 *)(param_1 + 0x48) - BINK handle
                //   dest = *(undefined4 *)(iVar1 + 0x14) - buffer pointer from BinkBufferLock
                //   destpitch = *(undefined4 *)(iVar1 + 0x18) - buffer pitch
                //   destheight = *(undefined4 *)(iVar1 + 4) - buffer height
                //   destx = 0, desty = 0
                //   flags = *(undefined4 *)(iVar1 + 0x10) - copy flags
                //
                // For our implementation, we need to read the buffer structure to get pitch
                // But for simplicity, we'll use frame width * 4 (RGBA) as pitch
                int destPitch = _frameWidth * 4; // RGBA = 4 bytes per pixel
                BinkApi.BinkCopyToBuffer(_binkHandle, bufferPtr, destPitch, _frameHeight, 0, 0, BinkApi.BINKCOPYVIDEO);

                // Read buffer data to our frame buffer for texture update
                // Based on decompilation: Buffer contains decoded frame data in RGBA format
                // The buffer pointer from BinkBufferLock points to the actual frame data
                int bufferSize = _frameWidth * _frameHeight * 4;
                Marshal.Copy(bufferPtr, _frameBuffer, 0, bufferSize);

                // Unlock buffer
                // Based on swkotor.exe: 0x00404c80 @ 0x00404c80 line 23 calls BinkBufferUnlock
                BinkApi.BinkBufferUnlock(_bufferHandle);
            }

            // Update texture with frame data
            // Based on swkotor.exe: 0x00404c80 @ 0x00404c80 line 27 calls BinkBufferBlit
            // Original: Blits buffer directly to screen, we update texture for rendering
            if (_frameTexture != null && _frameBuffer != null)
            {
                // Update texture with frame buffer data
                _frameTexture.SetData(_frameBuffer);
            }

            // Get destination rectangles for blitting
            // Based on swkotor.exe: 0x00404c80 @ 0x00404c80 line 25-26 calls BinkGetRects
            // Note: BinkGetRects may not be needed if we're using texture rendering instead of direct blit
            // IntPtr rects = BinkApi.BinkGetRects(_binkHandle, IntPtr.Zero);

            // Blit to screen (optional - we're using texture rendering instead)
            // Based on swkotor.exe: 0x00404c80 @ 0x00404c80 line 27 calls BinkBufferBlit
            // Original: Blits directly to screen, we use texture rendering for abstraction
            // BinkApi.BinkBufferBlit(_bufferHandle, rects, IntPtr.Zero);

            // Advance to next frame
            // Based on swkotor.exe: 0x00404c80 @ 0x00404c80 line 28 calls BinkNextFrame
            BinkApi.BinkNextFrame(_binkHandle);

            return true;
        }

        /// <summary>
        /// Waits for frame timing.
        /// Based on swkotor.exe: 0x00404c80 @ 0x00404c80 line 29-33 (BinkWait with Sleep loop)
        /// </summary>
        public void WaitForFrame()
        {
            if (_binkHandle == IntPtr.Zero)
            {
                return;
            }

            // Wait for frame timing
            // Based on swkotor.exe: 0x00404c80 @ 0x00404c80 line 29-33
            // BinkWait returns 1 if need to wait more, 0 if ready
            int waitResult = BinkApi.BinkWait(_binkHandle);
            while (waitResult == 1)
            {
                // Sleep for 1ms and check again
                // Based on decompilation: Sleep(1); iVar1 = _BinkWait_4(...);
                Thread.Sleep(1);
                waitResult = BinkApi.BinkWait(_binkHandle);
            }
        }

        /// <summary>
        /// Gets the frame texture for rendering.
        /// </summary>
        public IMovieTexture2D FrameTexture
        {
            get { return _frameTexture; }
        }

        /// <summary>
        /// Disposes resources.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            if (_bufferHandle != IntPtr.Zero)
            {
                BinkApi.BinkBufferClose(_bufferHandle);
                _bufferHandle = IntPtr.Zero;
            }

            if (_binkHandle != IntPtr.Zero)
            {
                BinkApi.BinkClose(_binkHandle);
                _binkHandle = IntPtr.Zero;
            }

            if (_frameTexture != null)
            {
                // Dispose texture
                _frameTexture.Dispose();
                _frameTexture = null;
            }

            _isDisposed = true;
        }
    }
}


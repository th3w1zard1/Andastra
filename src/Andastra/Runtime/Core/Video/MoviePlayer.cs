using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BioWare.NET.Common;
using BioWare.NET.Resource;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Video.Bink;

namespace Andastra.Runtime.Core.Video
{
    /// <summary>
    /// Movie player for BIK format video playback during module transitions.
    /// </summary>
    /// <remarks>
    /// Movie Player System:
    /// - Based on swkotor.exe/swkotor2.exe: CExoMoviePlayerInternal implementation
    /// - Located via string references: "CExoMoviePlayerInternal" @ 0x0073d773, "Oexomovieplayerinternal.cpp"
    /// - "SWMovieWindow" @ 0x0073d7fc (window class), "SW Movie Player Window" @ 0x0073d7e4 (window title)
    /// - Movie playback function: FUN_00404c80 @ 0x00404c80 (main playback loop)
    /// - Movie initialization: FUN_004053e0 @ 0x004053e0 (creates movie window, opens BIK file)
    /// - Bink API usage:
    ///   - BinkOpen @ 0x00405671 (opens BIK file)
    ///   - BinkBufferOpen @ 0x00405671 (creates buffer for video frames)
    ///   - BinkDoFrame @ 0x00404cb6 (decodes frame)
    ///   - BinkBufferLock @ 0x00404cb6 (locks buffer for writing)
    ///   - BinkCopyToBuffer @ 0x00404cbb (copies decoded frame to buffer)
    ///   - BinkBufferUnlock @ 0x00404cbb (unlocks buffer)
    ///   - BinkGetRects @ 0x00404cc0 (gets destination rectangles)
    ///   - BinkBufferBlit @ 0x00404cc5 (blits buffer to screen)
    ///   - BinkNextFrame @ 0x00404cca (advances to next frame)
    ///   - BinkWait @ 0x00404d26, 0x00404d38 (waits for frame timing, sleeps if needed)
    ///   - BinkClose @ 0x00404d26 (closes BIK file)
    /// - Movie file paths:
    ///   - "MOVIES:%s" @ 0x0073d7d8 (path format string)
    ///   - ".\\movies" @ 0x0074e004, "d:\\movies" @ 0x0074e010 (movie directory paths)
    ///   - "LIVE%d:movies\\%s" @ 0x0074ec3c (LIVE path format for CD-based installations)
    /// - Original implementation: Movies play fullscreen, blocking until completion
    /// - Playback loop processes Windows messages while playing
    /// - If movie playback fails, continues gracefully (doesn't block module transition)
    /// - Based on BINKW32.DLL API documentation and reverse-engineered implementation
    /// </remarks>
    public class MoviePlayer
    {
        private readonly IWorld _world;
        private readonly IMovieResourceProvider _resourceProvider;
        private readonly IMovieGraphicsDevice _graphicsDevice;
        private bool _isPlaying;

        /// <summary>
        /// Initializes a new instance of the MoviePlayer class.
        /// </summary>
        /// <param name="world">World instance for accessing game services.</param>
        /// <param name="resourceProvider">Resource provider for loading movie files.</param>
        /// <param name="graphicsDevice">Graphics device for rendering video frames.</param>
        public MoviePlayer(IWorld world, IMovieResourceProvider resourceProvider, IMovieGraphicsDevice graphicsDevice)
        {
            _world = world ?? throw new ArgumentNullException("world");
            _resourceProvider = resourceProvider ?? throw new ArgumentNullException("resourceProvider");
            _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException("graphicsDevice");
            _isPlaying = false;
        }

        /// <summary>
        /// Plays a BIK movie file, blocking until playback completes.
        /// Based on swkotor.exe: FUN_00404c80 @ 0x00404c80 (main playback loop)
        /// </summary>
        /// <param name="movieResRef">Movie resource reference (without .bik extension).</param>
        /// <param name="cancellationToken">Cancellation token to stop playback.</param>
        /// <returns>True if movie played successfully, false otherwise.</returns>
        public async Task<bool> PlayMovie(string movieResRef, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(movieResRef))
            {
                return false;
            }

            if (_isPlaying)
            {
                // Already playing a movie, skip
                return false;
            }

            _isPlaying = true;

            try
            {
                // Load movie file from resources
                // Based on swkotor.exe: Movie file loading (FUN_005fbbf0 @ 0x005fbbf0)
                // Path format: "MOVIES:%s" or "LIVE%d:movies\\%s"
                byte[] movieData = await LoadMovieFile(movieResRef, cancellationToken);
                if (movieData == null || movieData.Length == 0)
                {
                    System.Console.WriteLine("[MoviePlayer] Could not load movie file: '{0}'", movieResRef);
                    return false;
                }

                // Play movie using BIK decoder
                // Based on swkotor.exe: FUN_00404c80 @ 0x00404c80 (playback loop)
                bool success = await PlayBikMovie(movieData, cancellationToken);

                return success;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine("[MoviePlayer] Error playing movie '{0}': {1}", movieResRef, ex.Message);
                return false;
            }
            finally
            {
                _isPlaying = false;
            }
        }

        /// <summary>
        /// Loads movie file data from resources.
        /// Based on swkotor.exe: Movie file loading (FUN_005fbbf0 @ 0x005fbbf0)
        /// </summary>
        /// <param name="movieResRef">Movie resource reference.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Movie file data, or null if loading failed.</returns>
        private async Task<byte[]> LoadMovieFile(string movieResRef, CancellationToken cancellationToken)
        {
            if (_resourceProvider == null)
            {
                return null;
            }

            try
            {
                // Normalize resref (remove extension if present)
                string normalizedResRef = movieResRef;
                if (normalizedResRef.EndsWith(".bik", StringComparison.OrdinalIgnoreCase))
                {
                    normalizedResRef = normalizedResRef.Substring(0, normalizedResRef.Length - 4);
                }

                // Create resource identifier for BIK file
                ResourceIdentifier resourceId = new ResourceIdentifier(normalizedResRef, ResourceType.BIK);

                // Check if resource exists
                bool exists = await _resourceProvider.ExistsAsync(resourceId, cancellationToken);
                if (!exists)
                {
                    // Try with .bik extension
                    resourceId = new ResourceIdentifier(normalizedResRef + ".bik", ResourceType.BIK);
                    exists = await _resourceProvider.ExistsAsync(resourceId, cancellationToken);
                }

                if (!exists)
                {
                    System.Console.WriteLine("[MoviePlayer] Movie resource not found: '{0}'", normalizedResRef);
                    return null;
                }

                // Load movie file data
                byte[] movieData = await _resourceProvider.GetResourceBytesAsync(resourceId, cancellationToken);
                return movieData;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine("[MoviePlayer] Error loading movie file '{0}': {1}", movieResRef, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Plays BIK movie data using Bink decoder.
        /// Based on swkotor.exe: FUN_00404c80 @ 0x00404c80 (main playback loop)
        /// </summary>
        /// <param name="movieData">BIK movie file data.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if playback completed successfully, false otherwise.</returns>
        private async Task<bool> PlayBikMovie(byte[] movieData, CancellationToken cancellationToken)
        {
            if (movieData == null || movieData.Length == 0)
            {
                return false;
            }

            // Write movie data to temporary file (BinkOpen requires a file path, not memory)
            // Based on swkotor.exe: FUN_004053e0 @ 0x004053e0 opens BIK file from path
            string tempMoviePath = null;
            try
            {
                // Create temporary file for BIK data
                tempMoviePath = Path.Combine(Path.GetTempPath(), string.Format("andastra_movie_{0}.bik", Guid.NewGuid().ToString("N")));
                await File.WriteAllBytesAsync(tempMoviePath, movieData);

                // Create BIK decoder
                // Based on swkotor.exe: FUN_004053e0 @ 0x004053e0 (movie initialization)
                BikDecoder decoder = null;
                try
                {
                    decoder = new BikDecoder(tempMoviePath, _graphicsDevice);
                    decoder.Open();

                    // Main playback loop
                    // Based on swkotor.exe: FUN_00404c80 @ 0x00404c80 (playback loop)
                    // Loop until movie complete or cancelled
                    while (!decoder.IsComplete && !cancellationToken.IsCancellationRequested)
                    {
                        // Decode current frame
                        // Based on swkotor.exe: FUN_00404c80 @ 0x00404c80 line 15 calls BinkDoFrame
                        bool frameDecoded = decoder.DecodeFrame();
                        if (!frameDecoded)
                        {
                            // Frame decode failed or movie complete
                            break;
                        }

                        // Wait for frame timing
                        // Based on swkotor.exe: FUN_00404c80 @ 0x00404c80 line 29-33 (BinkWait with Sleep loop)
                        decoder.WaitForFrame();

                        // Process Windows messages
                        // Based on swkotor.exe: FUN_00404c80 @ 0x00404c80 line 35-40
                        // PeekMessage -> GetMessage -> TranslateMessage -> DispatchMessage
                        WindowsMessageProcessor.ProcessMessages(cancellationToken);

                        // Render frame to screen
                        // Based on swkotor.exe: FUN_00404c80 @ 0x00404c80 line 27 calls BinkBufferBlit
                        // The BinkBufferBlit is handled internally by BikDecoder, but we need to present to screen
                        RenderFrame(decoder);

                        // Yield to allow other tasks to run
                        await Task.Yield();
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine("[MoviePlayer] Error during BIK playback: {0}", ex.Message);
                    return false;
                }
                finally
                {
                    if (decoder != null)
                    {
                        decoder.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine("[MoviePlayer] Error writing temporary movie file: {0}", ex.Message);
                return false;
            }
            finally
            {
                // Clean up temporary file
                if (tempMoviePath != null && File.Exists(tempMoviePath))
                {
                    try
                    {
                        File.Delete(tempMoviePath);
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine("[MoviePlayer] Error deleting temporary movie file: {0}", ex.Message);
                    }
                }
            }
        }

        /// <summary>
        /// Renders the current frame to the screen.
        /// Based on swkotor.exe: FUN_00404c80 @ 0x00404c80 line 27 (BinkBufferBlit)
        /// </summary>
        /// <param name="decoder">BIK decoder with current frame.</param>
        private void RenderFrame(BikDecoder decoder)
        {
            if (decoder == null || decoder.FrameTexture == null)
            {
                return;
            }

            // Get viewport dimensions for fullscreen rendering
            MovieViewport viewport = _graphicsDevice.Viewport;
            int screenWidth = viewport.Width;
            int screenHeight = viewport.Height;

            // Clear screen to black
            MovieColor clearColor = new MovieColor(0, 0, 0, 255);
            _graphicsDevice.Clear(clearColor);

            // Render frame texture fullscreen
            // Based on swkotor.exe: FUN_00404c80 @ 0x00404c80 line 27 calls BinkBufferBlit
            // Original implementation: Blits buffer directly to screen using BinkBufferBlit
            // Our implementation: Uses sprite batch to render texture fullscreen
            using (IMovieSpriteBatch spriteBatch = _graphicsDevice.CreateSpriteBatch())
            {
                spriteBatch.Begin();

                // Calculate scaling to fill screen while maintaining aspect ratio
                float scaleX = (float)screenWidth / decoder.Width;
                float scaleY = (float)screenHeight / decoder.Height;
                float scale = Math.Min(scaleX, scaleY);

                int scaledWidth = (int)(decoder.Width * scale);
                int scaledHeight = (int)(decoder.Height * scale);
                int offsetX = (screenWidth - scaledWidth) / 2;
                int offsetY = (screenHeight - scaledHeight) / 2;

                // Draw frame texture centered and scaled
                // Based on swkotor.exe: FUN_00404c80 @ 0x00404c80 line 27 (BinkBufferBlit)
                // Original: Blits directly to screen, we use sprite batch for abstraction
                MovieRectangle destinationRect = new MovieRectangle(offsetX, offsetY, scaledWidth, scaledHeight);
                MovieColor whiteColor = new MovieColor(255, 255, 255, 255);
                spriteBatch.Draw(decoder.FrameTexture, destinationRect, whiteColor);

                spriteBatch.End();
            }
        }
    }
}


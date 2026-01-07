using System;
using System.IO;
using Andastra.Runtime.Graphics.Common.Enums;

namespace Andastra.Runtime.Graphics.Common.Interfaces
{
    /// <summary>
    /// Interface for graphics backends that support DirectStorage.
    /// DirectStorage enables high-speed, low-latency loading of assets directly to GPU memory,
    /// bypassing the CPU and reducing load times significantly.
    ///
    /// Based on DirectStorage API: https://devblogs.microsoft.com/directx/directstorage-is-coming-to-pc/
    /// </summary>
    /// <remarks>
    /// DirectStorage Backend Interface:
    /// - This is a modern graphics API feature (DirectX 12)
    /// - Original game graphics system: Primarily DirectX 9 (d3d9.dll @ 0x0080a6c0) or OpenGL (OPENGL32.dll @ 0x00809ce2)
    /// - Located via string references: "Render Window" @ 0x007b5680, "Graphics Options" @ 0x007b56a8
    /// - Original game did not support DirectStorage; this is a modern enhancement for faster asset loading
    /// - This interface: Provides DirectStorage abstraction for modern graphics APIs, not directly mapped to swkotor2.exe functions
    /// </remarks>
    public interface IDirectStorageBackend : ILowLevelBackend
    {
        /// <summary>
        /// Whether DirectStorage is available and supported.
        /// </summary>
        bool DirectStorageAvailable { get; }

        /// <summary>
        /// Creates a DirectStorage file handle for high-speed reading.
        /// </summary>
        /// <param name="filePath">Path to the file to open.</param>
        /// <param name="fileAccess">File access mode.</param>
        /// <returns>Handle to the DirectStorage file, or IntPtr.Zero on failure.</returns>
        IntPtr CreateDirectStorageFile(string filePath, FileAccess fileAccess);

        /// <summary>
        /// Creates a DirectStorage queue for submitting read requests.
        /// </summary>
        /// <param name="priority">Queue priority (normal, high, critical).</param>
        /// <param name="capacity">Maximum number of pending requests in the queue.</param>
        /// <returns>Handle to the DirectStorage queue, or IntPtr.Zero on failure.</returns>
        IntPtr CreateDirectStorageQueue(DirectStoragePriority priority, int capacity);

        /// <summary>
        /// Enqueues a read request to load data directly into GPU memory.
        /// </summary>
        /// <param name="queue">DirectStorage queue handle.</param>
        /// <param name="file">DirectStorage file handle.</param>
        /// <param name="offset">Offset in bytes from the start of the file.</param>
        /// <param name="size">Number of bytes to read.</param>
        /// <param name="destination">GPU buffer handle to write data to.</param>
        /// <param name="destinationOffset">Offset in the destination buffer.</param>
        /// <returns>Request ID for tracking completion, or -1 on failure.</returns>
        long EnqueueRead(IntPtr queue, IntPtr file, long offset, int size, IntPtr destination, int destinationOffset);

        /// <summary>
        /// Enqueues a read request to load data into a staging buffer (CPU-accessible).
        /// </summary>
        /// <param name="queue">DirectStorage queue handle.</param>
        /// <param name="file">DirectStorage file handle.</param>
        /// <param name="offset">Offset in bytes from the start of the file.</param>
        /// <param name="size">Number of bytes to read.</param>
        /// <param name="destination">CPU staging buffer handle.</param>
        /// <param name="destinationOffset">Offset in the destination buffer.</param>
        /// <returns>Request ID for tracking completion, or -1 on failure.</returns>
        long EnqueueReadToStaging(IntPtr queue, IntPtr file, long offset, int size, IntPtr destination, int destinationOffset);

        /// <summary>
        /// Submits all pending requests in the queue to the GPU.
        /// </summary>
        /// <param name="queue">DirectStorage queue handle.</param>
        void SubmitQueue(IntPtr queue);

        /// <summary>
        /// Checks if a request has completed.
        /// </summary>
        /// <param name="queue">DirectStorage queue handle.</param>
        /// <param name="requestId">Request ID returned from EnqueueRead.</param>
        /// <returns>True if the request has completed, false if still pending.</returns>
        bool IsRequestComplete(IntPtr queue, long requestId);

        /// <summary>
        /// Waits for a specific request to complete.
        /// </summary>
        /// <param name="queue">DirectStorage queue handle.</param>
        /// <param name="requestId">Request ID returned from EnqueueRead.</param>
        void WaitForRequest(IntPtr queue, long requestId);

        /// <summary>
        /// Waits for all pending requests in the queue to complete.
        /// </summary>
        /// <param name="queue">DirectStorage queue handle.</param>
        void WaitForQueue(IntPtr queue);
    }

    /// <summary>
    /// DirectStorage queue priority levels.
    /// Based on DSTORAGE_PRIORITY enum.
    /// </summary>
    public enum DirectStoragePriority
    {
        /// <summary>
        /// Normal priority - standard asset loading.
        /// </summary>
        Normal = 0,

        /// <summary>
        /// High priority - important assets that should load first.
        /// </summary>
        High = 1,

        /// <summary>
        /// Critical priority - blocking assets required for gameplay.
        /// </summary>
        Critical = 2
    }
}


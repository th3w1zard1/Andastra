using System;
using Andastra.Runtime.Graphics.Common.Enums;

namespace Andastra.Runtime.Graphics.Common.Interfaces
{
    /// <summary>
    /// Interface for graphics backends that support asynchronous compute (parallel GPU workloads).
    /// Async compute allows graphics and compute work to run simultaneously on different GPU engines,
    /// improving GPU utilization and overall performance.
    ///
    /// Based on DirectX 12 Async Compute: https://learn.microsoft.com/en-us/windows/win32/direct3d12/asynchronous-and-high-frequency-timing
    /// Based on Vulkan Multiple Queues: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/html/vkspec.html#devsandqueues-queues
    /// </summary>
    /// <remarks>
    /// Async Compute Backend Interface:
    /// - This is a modern graphics API feature (DirectX 12, Vulkan)
    /// - Original game graphics system: Primarily DirectX 9 (d3d9.dll @ 0x0080a6c0) or OpenGL (OPENGL32.dll @ 0x00809ce2)
    /// - Located via string references: "Render Window" @ 0x007b5680, "Graphics Options" @ 0x007b56a8
    /// - Original game did not support async compute; this is a modern enhancement for better GPU utilization
    /// - This interface: Provides async compute abstraction for modern graphics APIs, not directly mapped to swkotor2.exe functions
    /// </remarks>
    public interface IAsyncComputeBackend : ILowLevelBackend
    {
        /// <summary>
        /// Whether asynchronous compute is available and supported.
        /// </summary>
        bool AsyncComputeAvailable { get; }

        /// <summary>
        /// Creates a separate compute command queue for async compute work.
        /// </summary>
        /// <param name="priority">Queue priority (normal, high, realtime).</param>
        /// <returns>Handle to the compute queue, or IntPtr.Zero on failure.</returns>
        IntPtr CreateComputeQueue(ComputeQueuePriority priority);

        /// <summary>
        /// Creates a compute command list for async compute work.
        /// </summary>
        /// <param name="queue">Compute queue handle.</param>
        /// <returns>Handle to the command list, or IntPtr.Zero on failure.</returns>
        IntPtr CreateComputeCommandList(IntPtr queue);

        /// <summary>
        /// Submits compute work to the async compute queue (non-blocking).
        /// </summary>
        /// <param name="queue">Compute queue handle.</param>
        /// <param name="commandList">Command list handle containing compute commands.</param>
        void SubmitComputeQueue(IntPtr queue, IntPtr commandList);

        /// <summary>
        /// Sets a fence to signal when compute work completes.
        /// </summary>
        /// <param name="queue">Compute queue handle.</param>
        /// <param name="fence">Fence handle to signal.</param>
        /// <param name="value">Fence value to signal.</param>
        void SignalFence(IntPtr queue, IntPtr fence, ulong value);

        /// <summary>
        /// Waits for a fence value to be reached (blocks until compute work completes).
        /// </summary>
        /// <param name="fence">Fence handle to wait on.</param>
        /// <param name="value">Fence value to wait for.</param>
        void WaitForFence(IntPtr fence, ulong value);

        /// <summary>
        /// Creates a fence for synchronization between graphics and compute queues.
        /// </summary>
        /// <returns>Handle to the created fence, or IntPtr.Zero on failure.</returns>
        IntPtr CreateFence();

        /// <summary>
        /// Gets the current fence value.
        /// </summary>
        /// <param name="fence">Fence handle.</param>
        /// <returns>Current fence value.</returns>
        ulong GetFenceValue(IntPtr fence);
    }

    /// <summary>
    /// Compute queue priority levels.
    /// Based on D3D12_COMMAND_QUEUE_PRIORITY and VkQueuePriority.
    /// </summary>
    public enum ComputeQueuePriority
    {
        /// <summary>
        /// Normal priority - standard async compute work.
        /// </summary>
        Normal = 0,

        /// <summary>
        /// High priority - important compute work that should preempt normal work.
        /// </summary>
        High = 1,

        /// <summary>
        /// Realtime priority - critical compute work (use sparingly).
        /// </summary>
        Realtime = 2
    }
}


using StrideGraphics = Stride.Graphics;

namespace Andastra.Runtime.Stride.Graphics
{
    /// <summary>
    /// Extension methods for Stride GraphicsDevice to provide compatibility with older API.
    /// </summary>
    /// <remarks>
    /// TODO: STUB - ImmediateContext was removed from GraphicsDevice in newer Stride versions.
    /// Need to implement proper CommandList retrieval from GraphicsDevice.ResourceFactory or CommandListPool.
    /// For now, returns null to allow compilation. Code using this should handle null case.
    /// </remarks>
    public static class GraphicsDeviceExtensions
    {
        /// <summary>
        /// Gets the immediate command list for the graphics device.
        /// </summary>
        /// <remarks>
        /// TODO: STUB - This is a compatibility shim. In newer Stride versions, CommandList must be obtained
        /// from ResourceFactory.AllocateCommandList() or from a CommandListPool. This needs proper implementation.
        /// </remarks>
        [JetBrains.Annotations.CanBeNull]
        public static StrideGraphics.CommandList ImmediateContext(this StrideGraphics.GraphicsDevice device)
        {
            // TODO: STUB - Implement proper CommandList retrieval
            // In newer Stride, use: device.ResourceFactory.CreateCommandList() or get from CommandListPool
            // For now, return null to allow compilation - calling code must handle null
            return null;
        }
    }
}


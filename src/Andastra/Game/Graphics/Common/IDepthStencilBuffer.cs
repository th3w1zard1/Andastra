using System;

namespace Andastra.Runtime.Graphics
{
    /// <summary>
    /// Depth-stencil buffer interface.
    /// </summary>
    /// <remarks>
    /// Depth-Stencil Buffer Interface:
    /// - Based on swkotor2.exe DirectX depth-stencil buffer system
    /// - Located via string references: "GL_ARB_depth_texture" @ 0x007b8848, "m_sDepthTextureName" @ 0x007baaa8
    /// - "depth_texture" @ 0x007bab5c, "glDepthMask" @ 0x0080aa38, "glDepthFunc" @ 0x0080ad96
    /// - "glStencilOp" @ 0x0080a9f0, "glStencilMask" @ 0x0080aa0c, "glStencilFunc" @ 0x0080aa68
    /// - "glClearStencil" @ 0x0080ada4, "GL_EXT_stencil_two_side" @ 0x007b8a68, "glActiveStencilFaceEXT" @ 0x007b7624
    /// - Original implementation: DirectX 8/9 depth-stencil buffer for z-buffering and stencil operations
    /// - Depth buffer: Used for z-buffering to determine which pixels are visible
    /// - Stencil buffer: Used for masking and special effects (shadows, portals, etc.)
    /// - This interface: Abstraction layer for modern graphics APIs (DirectX 11/12, OpenGL, Vulkan)
    /// </remarks>
    public interface IDepthStencilBuffer : IDisposable
    {
        /// <summary>
        /// Gets the buffer width.
        /// </summary>
        int Width { get; }

        /// <summary>
        /// Gets the buffer height.
        /// </summary>
        int Height { get; }

        /// <summary>
        /// Gets the native buffer handle.
        /// </summary>
        IntPtr NativeHandle { get; }
    }
}


using System;

namespace Andastra.Runtime.Graphics
{
    /// <summary>
    /// Render target interface for off-screen rendering.
    /// </summary>
    /// <remarks>
    /// Render Target Interface:
    /// - Based on swkotor2.exe DirectX render target system
    /// - Located via string references: "WGL_NV_render_texture_rectangle" @ 0x007b880c, "WGL_ARB_render_texture" @ 0x007b8890
    /// - "m_sDepthTextureName" @ 0x007baaa8, "depth_texture" @ 0x007bab5c (depth render targets)
    /// - Original implementation: DirectX 8/9 render targets (IDirect3DSurface8/IDirect3DSurface9) for off-screen rendering
    /// - Render targets: Used for post-processing effects, shadow maps, reflections, etc.
    /// - This interface: Abstraction layer for modern graphics APIs (DirectX 11/12, OpenGL, Vulkan)
    /// - Note: Original game uses render targets for some effects, modern APIs have more advanced render target features
    /// </remarks>
    public interface IRenderTarget : IDisposable
    {
        /// <summary>
        /// Gets the render target width.
        /// </summary>
        int Width { get; }

        /// <summary>
        /// Gets the render target height.
        /// </summary>
        int Height { get; }

        /// <summary>
        /// Gets the color texture.
        /// </summary>
        ITexture2D ColorTexture { get; }

        /// <summary>
        /// Gets the depth-stencil buffer (if created).
        /// </summary>
        IDepthStencilBuffer DepthStencilBuffer { get; }

        /// <summary>
        /// Gets the native render target handle.
        /// </summary>
        IntPtr NativeHandle { get; }
    }
}


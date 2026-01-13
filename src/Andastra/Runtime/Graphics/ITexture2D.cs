using System;

namespace Andastra.Runtime.Graphics
{
    /// <summary>
    /// 2D texture interface.
    /// </summary>
    /// <remarks>
    /// Texture 2D Interface:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) texture loading and rendering system
    /// - Located via string references: "texturewidth" @ 0x007b6e98, "GL_ARB_texture_compression" @ 0x007b88fc
    /// - "GL_EXT_texture_compression_s3tc" @ 0x007b88dc (S3TC/DXT texture compression)
    /// - "GL_EXT_texture_filter_anisotropic" @ 0x007b8974 (anisotropic filtering)
    /// - "GL_EXT_texture_cube_map" @ 0x007b89dc (cube map textures)
    /// - "GL_EXT_texture_env_combine" @ 0x007b8a2c, "GL_ARB_multitexture" @ 0x007b8a48 (multitexturing)
    /// - "glActiveTextureARB" @ 0x007b8738, "glClientActiveTextureARB" @ 0x007b871c (multitexture functions)
    /// - "glBindTextureUnitParameterEXT" @ 0x007b7774 (texture binding)
    /// - Original implementation: Loads textures from TPC files, uses DirectX 8/9 texture objects (IDirect3DTexture8/IDirect3DTexture9)
    /// - Texture formats: TPC (Targa Packed Compressed) format with DXT/S3TC compression
    /// - This interface: Abstraction layer for modern graphics APIs (DirectX 11/12, OpenGL, Vulkan)
    /// </remarks>
    public interface ITexture2D : IDisposable
    {
        /// <summary>
        /// Gets the texture width.
        /// </summary>
        int Width { get; }

        /// <summary>
        /// Gets the texture height.
        /// </summary>
        int Height { get; }

        /// <summary>
        /// Gets the native texture handle.
        /// </summary>
        IntPtr NativeHandle { get; }

        /// <summary>
        /// Sets texture pixel data.
        /// </summary>
        /// <param name="data">Pixel data (RGBA format).</param>
        void SetData(byte[] data);

        /// <summary>
        /// Gets texture pixel data.
        /// </summary>
        /// <returns>Pixel data (RGBA format).</returns>
        byte[] GetData();
    }
}


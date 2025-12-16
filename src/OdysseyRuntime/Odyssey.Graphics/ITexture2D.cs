using System;

namespace Odyssey.Graphics
{
    /// <summary>
    /// 2D texture interface.
    /// </summary>
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


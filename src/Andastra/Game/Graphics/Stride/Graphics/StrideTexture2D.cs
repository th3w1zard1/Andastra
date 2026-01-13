using System;
using Andastra.Runtime.Graphics;
using Stride.Engine;
using StrideGraphics = global::Stride.Graphics;

namespace Andastra.Game.Stride.Graphics
{
    /// <summary>
    /// Stride implementation of ITexture2D.
    /// </summary>
    public class StrideTexture2D : ITexture2D
    {
        private readonly global::Stride.Graphics.Texture _texture;
        private readonly global::Stride.Graphics.CommandList _graphicsContext;

        internal global::Stride.Graphics.Texture Texture => _texture;

        public StrideTexture2D(global::Stride.Graphics.Texture texture, global::Stride.Graphics.CommandList graphicsContext = null)
        {
            _texture = texture ?? throw new ArgumentNullException(nameof(texture));
            _graphicsContext = graphicsContext;
        }

        public int Width => _texture.Width;

        public int Height => _texture.Height;

        public IntPtr NativeHandle
        {
            get
            {
                // Stride Texture doesn't expose NativeDeviceTexture directly
                // Return IntPtr.Zero as Stride manages native resources internally
                return IntPtr.Zero;
            }
        }

        public void SetData(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var colorData = new global::Stride.Core.Mathematics.Color[data.Length / 4];
            for (int i = 0; i < colorData.Length; i++)
            {
                int offset = i * 4;
                colorData[i] = new global::Stride.Core.Mathematics.Color(data[offset], data[offset + 1], data[offset + 2], data[offset + 3]);
            }

            if (_graphicsContext == null)
            {
                throw new InvalidOperationException("CommandList is required for Texture.SetData() but is not available. StrideTexture2D must be created with a valid CommandList.");
            }
            _texture.SetData(_graphicsContext, colorData);
        }

        public byte[] GetData()
        {
            var colorData = new global::Stride.Core.Mathematics.Color[_texture.Width * _texture.Height];
            if (_graphicsContext == null)
            {
                throw new InvalidOperationException("CommandList is required for Texture.GetData() but is not available. StrideTexture2D must be created with a valid CommandList.");
            }
            _texture.GetData(_graphicsContext, colorData);

            var byteData = new byte[colorData.Length * 4];
            for (int i = 0; i < colorData.Length; i++)
            {
                int offset = i * 4;
                byteData[offset] = colorData[i].R;
                byteData[offset + 1] = colorData[i].G;
                byteData[offset + 2] = colorData[i].B;
                byteData[offset + 3] = colorData[i].A;
            }

            return byteData;
        }

        public void Dispose()
        {
            _texture?.Dispose();
        }
    }
}


using System;
using Stride.Graphics;
using Odyssey.Graphics;

namespace Odyssey.Stride.Graphics
{
    /// <summary>
    /// Stride implementation of ITexture2D.
    /// </summary>
    public class StrideTexture2D : ITexture2D
    {
        private readonly Texture2D _texture;

        internal Texture2D Texture => _texture;

        public StrideTexture2D(Texture2D texture)
        {
            _texture = texture ?? throw new ArgumentNullException(nameof(texture));
        }

        public int Width => _texture.Width;

        public int Height => _texture.Height;

        public IntPtr NativeHandle => _texture.NativeDeviceTexture;

        public void SetData(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var colorData = new Stride.Core.Mathematics.Color[data.Length / 4];
            for (int i = 0; i < colorData.Length; i++)
            {
                int offset = i * 4;
                colorData[i] = new Stride.Core.Mathematics.Color(data[offset], data[offset + 1], data[offset + 2], data[offset + 3]);
            }

            _texture.SetData(_texture.GraphicsDevice.ImmediateContext, colorData);
        }

        public byte[] GetData()
        {
            var colorData = new Stride.Core.Mathematics.Color[_texture.Width * _texture.Height];
            _texture.GetData(_texture.GraphicsDevice.ImmediateContext, colorData);

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


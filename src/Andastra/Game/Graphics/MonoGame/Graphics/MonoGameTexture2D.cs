using System;
using Andastra.Runtime.Graphics;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Game.Graphics.MonoGame.Graphics
{
    /// <summary>
    /// MonoGame implementation of ITexture2D.
    /// </summary>
    public class MonoGameTexture2D : ITexture2D
    {
        private readonly Texture2D _texture;

        /// <summary>
        /// Gets the underlying MonoGame Texture2D (for interop use).
        /// </summary>
        public Texture2D Texture => _texture;

        public MonoGameTexture2D(Texture2D texture)
        {
            _texture = texture ?? throw new ArgumentNullException(nameof(texture));
        }

        public int Width => _texture.Width;

        public int Height => _texture.Height;

        // MonoGame Texture2D doesn't expose Handle directly - return IntPtr.Zero as fallback
        public IntPtr NativeHandle => IntPtr.Zero;

        public void SetData(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var colorData = new Microsoft.Xna.Framework.Color[data.Length / 4];
            for (int i = 0; i < colorData.Length; i++)
            {
                int offset = i * 4;
                colorData[i] = new Microsoft.Xna.Framework.Color(data[offset], data[offset + 1], data[offset + 2], data[offset + 3]);
            }

            _texture.SetData(colorData);
        }

        public byte[] GetData()
        {
            var colorData = new Microsoft.Xna.Framework.Color[_texture.Width * _texture.Height];
            _texture.GetData(colorData);

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


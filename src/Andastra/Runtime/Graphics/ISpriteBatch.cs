namespace Andastra.Runtime.Graphics
{
    /// <summary>
    /// Sprite batch interface for 2D rendering.
    /// </summary>
    /// <remarks>
    /// Sprite Batch Interface:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) 2D rendering system
    /// - Located via string references: Original game uses DirectX 8/9 for 2D sprite rendering
    /// - GUI rendering: "gui3D_room" @ 0x007cc144 (3D GUI room), "Render Window" @ 0x007b5680
    /// - Original implementation: Renders 2D sprites (GUI elements, text, HUD) using DirectX immediate mode
    /// - Sprite rendering: Uses textured quads for 2D rendering (GUI panels, buttons, text, etc.)
    /// - Text rendering: Uses bitmap fonts (dialogfont16x16.tga) rendered as sprites
    /// - This interface: Abstraction layer for modern sprite batching (MonoGame SpriteBatch, Stride SpriteBatch)
    /// - Note: Modern sprite batching optimizes draw calls by batching sprites, original game uses immediate mode
    /// </remarks>
    public interface ISpriteBatch : System.IDisposable
    {
        /// <summary>
        /// Begins sprite batch rendering.
        /// </summary>
        /// <param name="sortMode">Sprite sort mode.</param>
        /// <param name="blendState">Blend state.</param>
        void Begin(SpriteSortMode sortMode = SpriteSortMode.Deferred, BlendState blendState = null);

        /// <summary>
        /// Ends sprite batch rendering.
        /// </summary>
        void End();

        /// <summary>
        /// Draws a texture.
        /// </summary>
        /// <param name="texture">Texture to draw.</param>
        /// <param name="position">Position.</param>
        /// <param name="color">Tint color.</param>
        void Draw(ITexture2D texture, Vector2 position, Color color);

        /// <summary>
        /// Draws a texture with destination rectangle.
        /// </summary>
        /// <param name="texture">Texture to draw.</param>
        /// <param name="destinationRectangle">Destination rectangle.</param>
        /// <param name="color">Tint color.</param>
        void Draw(ITexture2D texture, Rectangle destinationRectangle, Color color);

        /// <summary>
        /// Draws a texture with source rectangle.
        /// </summary>
        /// <param name="texture">Texture to draw.</param>
        /// <param name="position">Position.</param>
        /// <param name="sourceRectangle">Source rectangle (null for entire texture).</param>
        /// <param name="color">Tint color.</param>
        void Draw(ITexture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color);

        /// <summary>
        /// Draws a texture with full transform.
        /// </summary>
        /// <param name="texture">Texture to draw.</param>
        /// <param name="destinationRectangle">Destination rectangle.</param>
        /// <param name="sourceRectangle">Source rectangle (null for entire texture).</param>
        /// <param name="color">Tint color.</param>
        /// <param name="rotation">Rotation in radians.</param>
        /// <param name="origin">Rotation origin.</param>
        /// <param name="effects">Sprite effects.</param>
        /// <param name="layerDepth">Layer depth (0.0 front to 1.0 back).</param>
        void Draw(ITexture2D texture, Rectangle destinationRectangle, Rectangle? sourceRectangle, Color color, float rotation, Vector2 origin, SpriteEffects effects, float layerDepth);

        /// <summary>
        /// Draws text using a font.
        /// </summary>
        /// <param name="font">Font to use.</param>
        /// <param name="text">Text to draw.</param>
        /// <param name="position">Position.</param>
        /// <param name="color">Text color.</param>
        void DrawString(IFont font, string text, Vector2 position, Color color);
    }

    /// <summary>
    /// 2D vector structure.
    /// </summary>
    public struct Vector2
    {
        public float X;
        public float Y;

        public Vector2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static Vector2 Zero => new Vector2(0, 0);
        public static Vector2 One => new Vector2(1, 1);

        public static Vector2 operator +(Vector2 left, Vector2 right)
        {
            return new Vector2(left.X + right.X, left.Y + right.Y);
        }
    }

    /// <summary>
    /// Sprite sort mode.
    /// </summary>
    public enum SpriteSortMode
    {
        Deferred,
        Immediate,
        Texture,
        BackToFront,
        FrontToBack
    }

    /// <summary>
    /// Sprite effects (flipping).
    /// </summary>
    public enum SpriteEffects
    {
        None = 0,
        FlipHorizontally = 1,
        FlipVertically = 2
    }

    /// <summary>
    /// Blend state for sprite rendering.
    /// </summary>
    public class BlendState
    {
        public static BlendState Opaque = new BlendState { AlphaBlend = false };
        public static BlendState Default = new BlendState { AlphaBlend = true };
        public static BlendState AdditiveBlend = new BlendState { AlphaBlend = true, Additive = true };

        public bool AlphaBlend { get; set; }
        public bool Additive { get; set; }
    }

    /// <summary>
    /// Rectangle structure.
    /// </summary>
    public struct Rectangle
    {
        public int X;
        public int Y;
        public int Width;
        public int Height;

        public Rectangle(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public int Left => X;
        public int Top => Y;
        public int Right => X + Width;
        public int Bottom => Y + Height;
    }
}


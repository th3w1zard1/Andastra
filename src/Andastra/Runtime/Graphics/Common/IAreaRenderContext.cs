using System.Numerics;
using JetBrains.Annotations;

namespace Andastra.Runtime.Graphics
{
    /// <summary>
    /// Rendering context interface for area rendering.
    /// Provides access to graphics services needed for area rendering.
    /// </summary>
    /// <remarks>
    /// Area Render Context:
    /// - Based on swkotor2.exe: Area rendering uses graphics device, room mesh renderer, and basic effect
    /// - Provides unified access to rendering services for area rendering
    /// - Set by game loop before calling area.Render()
    /// - Contains graphics device, room mesh renderer, basic effect, camera matrices, and camera position
    /// </remarks>
    [PublicAPI]
    public interface IAreaRenderContext
    {
        /// <summary>
        /// Gets the graphics device for rendering operations.
        /// </summary>
        IGraphicsDevice GraphicsDevice { get; }

        /// <summary>
        /// Gets the room mesh renderer for loading and rendering room meshes.
        /// </summary>
        IRoomMeshRenderer RoomMeshRenderer { get; }

        /// <summary>
        /// Gets the basic effect for 3D rendering.
        /// </summary>
        IBasicEffect BasicEffect { get; }

        /// <summary>
        /// Gets the view matrix (camera transformation).
        /// </summary>
        Matrix4x4 ViewMatrix { get; }

        /// <summary>
        /// Gets the projection matrix (perspective transformation).
        /// </summary>
        Matrix4x4 ProjectionMatrix { get; }

        /// <summary>
        /// Gets the camera position in world space.
        /// </summary>
        Vector3 CameraPosition { get; }
    }
}


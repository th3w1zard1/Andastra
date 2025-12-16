using System;
using System.Numerics;

namespace Odyssey.Graphics
{
    /// <summary>
    /// Matrix helper class for common matrix operations.
    /// Provides unified matrix operations across MonoGame and Stride.
    /// </summary>
    public static class MatrixHelper
    {
        /// <summary>
        /// Creates a look-at view matrix.
        /// </summary>
        /// <param name="cameraPosition">Camera position.</param>
        /// <param name="cameraTarget">Camera target.</param>
        /// <param name="cameraUpVector">Camera up vector.</param>
        /// <returns>View matrix.</returns>
        public static Matrix4x4 CreateLookAt(Vector3 cameraPosition, Vector3 cameraTarget, Vector3 cameraUpVector)
        {
            return Matrix4x4.CreateLookAt(cameraPosition, cameraTarget, cameraUpVector);
        }

        /// <summary>
        /// Creates a perspective projection matrix.
        /// </summary>
        /// <param name="fieldOfView">Field of view in radians.</param>
        /// <param name="aspectRatio">Aspect ratio (width/height).</param>
        /// <param name="nearPlaneDistance">Near plane distance.</param>
        /// <param name="farPlaneDistance">Far plane distance.</param>
        /// <returns>Projection matrix.</returns>
        public static Matrix4x4 CreatePerspectiveFieldOfView(float fieldOfView, float aspectRatio, float nearPlaneDistance, float farPlaneDistance)
        {
            return Matrix4x4.CreatePerspectiveFieldOfView(fieldOfView, aspectRatio, nearPlaneDistance, farPlaneDistance);
        }

        /// <summary>
        /// Creates a translation matrix.
        /// </summary>
        /// <param name="position">Translation vector.</param>
        /// <returns>Translation matrix.</returns>
        public static Matrix4x4 CreateTranslation(Vector3 position)
        {
            return Matrix4x4.CreateTranslation(position);
        }

        /// <summary>
        /// Creates a rotation matrix from a quaternion.
        /// </summary>
        /// <param name="quaternion">Rotation quaternion.</param>
        /// <returns>Rotation matrix.</returns>
        public static Matrix4x4 CreateFromQuaternion(Quaternion quaternion)
        {
            return Matrix4x4.CreateFromQuaternion(quaternion);
        }

        /// <summary>
        /// Creates a scale matrix.
        /// </summary>
        /// <param name="scale">Scale vector.</param>
        /// <returns>Scale matrix.</returns>
        public static Matrix4x4 CreateScale(Vector3 scale)
        {
            return Matrix4x4.CreateScale(scale);
        }

        /// <summary>
        /// Creates a world matrix from position, rotation, and scale.
        /// </summary>
        /// <param name="position">Position.</param>
        /// <param name="rotation">Rotation quaternion.</param>
        /// <param name="scale">Scale.</param>
        /// <returns>World matrix.</returns>
        public static Matrix4x4 CreateWorld(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            return Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(position);
        }
    }
}


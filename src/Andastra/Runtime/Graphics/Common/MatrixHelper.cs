using System;
using System.Numerics;

namespace Andastra.Runtime.Graphics
{
    /// <summary>
    /// Matrix helper class for common matrix operations.
    /// Provides unified matrix operations across MonoGame and Stride.
    /// </summary>
    /// <remarks>
    /// Matrix Helper:
    /// - Based on swkotor2.exe matrix transformation system
    /// - Located via string references: "Render Window" @ 0x007b5680, "Graphics Options" @ 0x007b56a8
    /// - Original game graphics system: Primarily DirectX 9 (d3d9.dll @ 0x0080a6c0) or OpenGL (OPENGL32.dll @ 0x00809ce2)
    /// - Matrix operations: World, view, projection matrices for 3D rendering
    /// - World matrix: Transforms objects from model space to world space (position, rotation, scale)
    /// - View matrix: Transforms from world space to camera space (camera position and orientation)
    /// - Projection matrix: Transforms from camera space to screen space (perspective/orthographic projection)
    /// - Original implementation: Uses DirectX 8/9 matrix operations (D3DXMatrixLookAtLH, D3DXMatrixPerspectiveFovLH, D3DXMatrixTranslation, etc.)
    /// - This helper: Abstraction layer for modern matrix operations (System.Numerics.Matrix4x4)
    /// </remarks>
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

        /// <summary>
        /// Creates a rotation matrix around the Y axis.
        /// </summary>
        /// <param name="radians">Rotation angle in radians.</param>
        /// <returns>Rotation matrix.</returns>
        public static Matrix4x4 CreateRotationY(float radians)
        {
            return Matrix4x4.CreateRotationY(radians);
        }

        /// <summary>
        /// Converts degrees to radians.
        /// </summary>
        /// <param name="degrees">Angle in degrees.</param>
        /// <returns>Angle in radians.</returns>
        public static float ToRadians(float degrees)
        {
            return (float)(degrees * Math.PI / 180.0);
        }
    }
}


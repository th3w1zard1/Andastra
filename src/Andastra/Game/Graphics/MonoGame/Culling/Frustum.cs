using System;
using System.Numerics;
using Microsoft.Xna.Framework;

namespace Andastra.Game.Graphics.MonoGame.Culling
{
    /// <summary>
    /// View frustum for culling objects outside the camera's view.
    /// 
    /// The frustum is defined by 6 planes extracted from the view-projection matrix.
    /// Objects can be tested against these planes to determine visibility.
    /// 
    /// Implementation based on Gribb/Hartmann method for frustum plane extraction.
    /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/gl/scene/frustum.py:36
    /// Original: class Frustum with planes and update_from_camera method
    /// </summary>
    /// <remarks>
    /// Frustum Culling System:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) rendering and culling system
    /// - Located via string references: "VISIBLEVALUE" @ 0x007b6a58 (visibility value for culling)
    /// - "VisibleModel" @ 0x007c1c98 (visible model flag), "NonCull" @ 0x007ccb1c (non-cullable objects)
    /// - "IsBodyBagVisible" @ 0x007c1ff0 (body bag visibility flag)
    /// - VIS file format: "%s/%s.VIS" @ 0x007b972c (VIS file path format), "visasmarr" @ 0x007bf720 (VIS file reference)
    /// - Original implementation: KOTOR performs frustum culling for rooms and objects
    /// - Frustum planes: 6 planes (left, right, bottom, top, near, far) extracted from view-projection matrix
    /// - Gribb/Hartmann method: Efficient plane extraction from combined view-projection matrix
    /// - Used for: Culling objects outside camera view, optimizing rendering performance
    /// - Combined with VIS files (room visibility) and distance culling for comprehensive culling pipeline
    /// - VIS files contain room-to-room visibility data for efficient room culling
    /// </remarks>
    public class Frustum
    {
        /// <summary>
        /// Frustum plane indices.
        /// </summary>
        public enum PlaneIndex
        {
            Left = 0,
            Right = 1,
            Bottom = 2,
            Top = 3,
            Near = 4,
            Far = 5
        }

        private readonly System.Numerics.Vector4[] _planes;
        private Matrix _cachedViewProjection;
        private bool _planesValid;

        /// <summary>
        /// Gets the frustum planes (normal.x, normal.y, normal.z, distance).
        /// </summary>
        public System.Numerics.Vector4[] Planes
        {
            get { return _planes; }
        }

        /// <summary>
        /// Initializes a new frustum.
        /// </summary>
        public Frustum()
        {
            _planes = new System.Numerics.Vector4[6];
            _cachedViewProjection = Matrix.Identity;
            _planesValid = false;
        }

        /// <summary>
        /// Extracts frustum planes from view-projection matrix.
        /// Uses Gribb/Hartmann method to extract planes directly from combined matrix.
        /// Based on PyKotor frustum.py:55 update_from_camera method
        /// </summary>
        /// <param name="viewMatrix">View transformation matrix.</param>
        /// <param name="projectionMatrix">Projection matrix.</param>
        public void UpdateFromMatrices(Matrix viewMatrix, Matrix projectionMatrix)
        {
            Matrix viewProjection = viewMatrix * projectionMatrix;

            // Simple check if matrices changed (avoid recalculation if unchanged)
            if (_planesValid && _cachedViewProjection == viewProjection)
            {
                return;
            }

            _cachedViewProjection = viewProjection;
            _planesValid = true;

            // Extract planes using Gribb/Hartmann method
            // Left plane: row3 + row0
            _planes[(int)PlaneIndex.Left] = NormalizePlane(new System.Numerics.Vector4(
                viewProjection.M14 + viewProjection.M11,
                viewProjection.M24 + viewProjection.M21,
                viewProjection.M34 + viewProjection.M31,
                viewProjection.M44 + viewProjection.M41
            ));

            // Right plane: row3 - row0
            _planes[(int)PlaneIndex.Right] = NormalizePlane(new System.Numerics.Vector4(
                viewProjection.M14 - viewProjection.M11,
                viewProjection.M24 - viewProjection.M21,
                viewProjection.M34 - viewProjection.M31,
                viewProjection.M44 - viewProjection.M41
            ));

            // Bottom plane: row3 + row1
            _planes[(int)PlaneIndex.Bottom] = NormalizePlane(new System.Numerics.Vector4(
                viewProjection.M14 + viewProjection.M12,
                viewProjection.M24 + viewProjection.M22,
                viewProjection.M34 + viewProjection.M32,
                viewProjection.M44 + viewProjection.M42
            ));

            // Top plane: row3 - row1
            _planes[(int)PlaneIndex.Top] = NormalizePlane(new System.Numerics.Vector4(
                viewProjection.M14 - viewProjection.M12,
                viewProjection.M24 - viewProjection.M22,
                viewProjection.M34 - viewProjection.M32,
                viewProjection.M44 - viewProjection.M42
            ));

            // Near plane: row3 + row2
            _planes[(int)PlaneIndex.Near] = NormalizePlane(new System.Numerics.Vector4(
                viewProjection.M14 + viewProjection.M13,
                viewProjection.M24 + viewProjection.M23,
                viewProjection.M34 + viewProjection.M33,
                viewProjection.M44 + viewProjection.M43
            ));

            // Far plane: row3 - row2
            _planes[(int)PlaneIndex.Far] = NormalizePlane(new System.Numerics.Vector4(
                viewProjection.M14 - viewProjection.M13,
                viewProjection.M24 - viewProjection.M23,
                viewProjection.M34 - viewProjection.M33,
                viewProjection.M44 - viewProjection.M43
            ));
        }

        /// <summary>
        /// Normalizes a plane equation.
        /// </summary>
        private System.Numerics.Vector4 NormalizePlane(System.Numerics.Vector4 plane)
        {
            float length = (float)Math.Sqrt(plane.X * plane.X + plane.Y * plane.Y + plane.Z * plane.Z);
            if (length > 1e-10f)
            {
                float invLength = 1.0f / length;
                return new System.Numerics.Vector4(
                    plane.X * invLength,
                    plane.Y * invLength,
                    plane.Z * invLength,
                    plane.W * invLength
                );
            }
            // Degenerate plane - set to default that won't cull anything
            return new System.Numerics.Vector4(0.0f, 0.0f, 1.0f, 1e10f);
        }

        /// <summary>
        /// Tests if a point is inside the frustum.
        /// Based on PyKotor frustum.py:152 point_in_frustum method
        /// </summary>
        public bool PointInFrustum(System.Numerics.Vector3 point)
        {
            if (!_planesValid)
            {
                return true;
            }

            for (int i = 0; i < 6; i++)
            {
                System.Numerics.Vector4 plane = _planes[i];
                float distance = plane.X * point.X + plane.Y * point.Y + plane.Z * point.Z + plane.W;
                if (distance < 0)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Tests if a bounding sphere intersects the frustum.
        /// This is the primary culling test used for render objects.
        /// Based on PyKotor frustum.py:168 sphere_in_frustum method
        /// </summary>
        public bool
        SphereInFrustum(System.Numerics.Vector3 center, float radius)
        {
            if (!_planesValid)
            {
                return true;
            }

            for (int i = 0; i < 6; i++)
            {
                System.Numerics.Vector4 plane = _planes[i];
                float distance = plane.X * center.X + plane.Y * center.Y + plane.Z * center.Z + plane.W;
                // If sphere is completely behind the plane, it's outside
                if (distance < -radius)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Tests if an axis-aligned bounding box intersects the frustum.
        /// Uses plane-AABB intersection test for accurate culling.
        /// Based on PyKotor frustum.py:188 aabb_in_frustum method
        /// </summary>
        public bool AabbInFrustum(System.Numerics.Vector3 minPoint, System.Numerics.Vector3 maxPoint)
        {
            if (!_planesValid)
            {
                return true;
            }

            for (int i = 0; i < 6; i++)
            {
                System.Numerics.Vector4 plane = _planes[i];
                // Find the positive vertex (furthest in plane normal direction)
                System.Numerics.Vector3 pVertex = new System.Numerics.Vector3(
                    plane.X >= 0 ? maxPoint.X : minPoint.X,
                    plane.Y >= 0 ? maxPoint.Y : minPoint.Y,
                    plane.Z >= 0 ? maxPoint.Z : minPoint.Z
                );

                // If the positive vertex is behind the plane, AABB is outside
                if (plane.X * pVertex.X + plane.Y * pVertex.Y + plane.Z * pVertex.Z + plane.W < 0)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Gets the minimum distance from sphere to any frustum plane.
        /// Useful for level-of-detail calculations.
        /// Based on PyKotor frustum.py:218 sphere_in_frustum_distance method
        /// </summary>
        public float SphereInFrustumDistance(System.Numerics.Vector3 center, float radius)
        {
            if (!_planesValid)
            {
                return float.MaxValue;
            }

            float minDistance = float.MaxValue;

            for (int i = 0; i < 6; i++)
            {
                System.Numerics.Vector4 plane = _planes[i];
                float distance = plane.X * center.X + plane.Y * center.Y + plane.Z * center.Z + plane.W;
                float adjustedDistance = distance + radius;
                minDistance = Math.Min(minDistance, adjustedDistance);
            }

            return minDistance;
        }
    }

    /// <summary>
    /// Statistics for frustum culling performance monitoring.
    /// Based on PyKotor frustum.py:240 CullingStats class
    /// </summary>
    public class CullingStats
    {
        /// <summary>
        /// Total objects tested.
        /// </summary>
        public int TotalObjects { get; private set; }

        /// <summary>
        /// Objects culled (outside frustum).
        /// </summary>
        public int CulledObjects { get; private set; }

        /// <summary>
        /// Objects visible (inside frustum).
        /// </summary>
        public int VisibleObjects { get; private set; }

        /// <summary>
        /// Frame count.
        /// </summary>
        public int FrameCount { get; private set; }

        /// <summary>
        /// Gets the percentage of objects culled.
        /// </summary>
        public float CullRate
        {
            get
            {
                if (TotalObjects == 0)
                {
                    return 0.0f;
                }
                return (CulledObjects / (float)TotalObjects) * 100.0f;
            }
        }

        /// <summary>
        /// Resets statistics for a new frame.
        /// </summary>
        public void Reset()
        {
            TotalObjects = 0;
            CulledObjects = 0;
            VisibleObjects = 0;
        }

        /// <summary>
        /// Records an object's visibility result.
        /// </summary>
        public void RecordObject(bool visible)
        {
            TotalObjects++;
            if (visible)
            {
                VisibleObjects++;
            }
            else
            {
                CulledObjects++;
            }
        }

        /// <summary>
        /// Marks end of frame for statistics.
        /// </summary>
        public void EndFrame()
        {
            FrameCount++;
        }
    }
}


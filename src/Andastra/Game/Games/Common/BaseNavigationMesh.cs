using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Games.Common
{
    /// <summary>
    /// Base class for navigation mesh implementations across all BioWare engines.
    /// </summary>
    /// <remarks>
    /// Base Navigation Mesh:
    /// - Contains common line-of-sight logic shared across all engines
    /// - Engine-specific implementations inherit from this base class
    /// - Common patterns: raycast-based line of sight, walkable surface checks, tolerance handling
    /// 
    /// Cross-engine analysis:
    /// - Odyssey (swkotor.exe, swkotor2.exe): Walkmesh-based line of sight with walkable face checks
    /// - Aurora (nwmain.exe): Tile-based line of sight with blocking tile checks
    /// - Eclipse (daorigins.exe, DragonAge2.exe): Dynamic obstacle-aware line of sight
    /// - Infinity (BaldurGate.exe, IcewindDale.exe, PlanescapeTorment.exe): Similar to Eclipse with physics integration
    /// 
    /// Common line-of-sight algorithm:
    /// 1. Handle edge case: same point (always has line of sight)
    /// 2. Normalize direction vector
    /// 3. Perform raycast against navigation mesh
    /// 4. Check if hit is very close to destination (tolerance check)
    /// 5. Check if hit geometry blocks line of sight (engine-specific)
    /// 6. Return true if no blocking geometry found
    /// </remarks>
    public abstract class BaseNavigationMesh : INavigationMesh
    {
        /// <summary>
        /// Tolerance for hit distance checks (0.5 unit tolerance for precision).
        /// </summary>
        protected const float LineOfSightTolerance = 0.5f;

        /// <summary>
        /// Checks line of sight between two points using common algorithm.
        /// </summary>
        /// <remarks>
        /// Common implementation shared across all engines:
        /// - Handles same-point edge case
        /// - Performs raycast
        /// - Checks tolerance for destination proximity
        /// - Delegates engine-specific blocking checks to CheckHitBlocksLineOfSight
        /// </remarks>
        public bool HasLineOfSight(Vector3 start, Vector3 end)
        {
            // Handle edge case: same point
            Vector3 direction = end - start;
            float distance = direction.Length();
            if (distance < 1e-6f)
            {
                return true; // Same point, line of sight is clear
            }

            // Normalize direction for raycast
            Vector3 normalizedDir = direction / distance;

            // Perform raycast to check for obstructions
            Vector3 hitPoint;
            int hitFace;
            if (Raycast(start, normalizedDir, distance, out hitPoint, out hitFace))
            {
                // A hit was found - check if it blocks line of sight

                // Calculate distances
                float distToHit = Vector3.Distance(start, hitPoint);
                float distToDest = distance;

                // If hit is very close to destination (within tolerance), consider line of sight clear
                // This handles cases where the raycast hits the destination geometry itself
                if (distToDest - distToHit < LineOfSightTolerance)
                {
                    return true; // Hit is at or very close to destination, line of sight is clear
                }

                // Engine-specific check: does this hit block line of sight?
                return CheckHitBlocksLineOfSight(hitPoint, hitFace, start, end);
            }

            // No hit found - check for engine-specific dynamic obstacles
            return CheckDynamicObstacles(start, end, normalizedDir, distance);
        }

        /// <summary>
        /// Engine-specific check to determine if a hit blocks line of sight.
        /// </summary>
        /// <param name="hitPoint">Point where raycast hit geometry.</param>
        /// <param name="hitFace">Face index that was hit (-1 if not applicable).</param>
        /// <param name="start">Start point of line of sight check.</param>
        /// <param name="end">End point of line of sight check.</param>
        /// <returns>True if line of sight is clear, false if blocked.</returns>
        /// <remarks>
        /// Engine-specific implementations:
        /// - Odyssey: Checks if hit face is walkable (walkable faces don't block)
        /// - Aurora: Checks if hit tile blocks line of sight
        /// - Eclipse: Checks destructible modifications and walkable faces
        /// - Infinity: Similar to Eclipse with physics integration
        /// </remarks>
        protected abstract bool CheckHitBlocksLineOfSight(Vector3 hitPoint, int hitFace, Vector3 start, Vector3 end);

        /// <summary>
        /// Engine-specific check for dynamic obstacles that may block line of sight.
        /// </summary>
        /// <param name="start">Start point of line of sight check.</param>
        /// <param name="end">End point of line of sight check.</param>
        /// <param name="direction">Normalized direction vector.</param>
        /// <param name="distance">Distance between start and end.</param>
        /// <returns>True if line of sight is clear, false if blocked by dynamic obstacles.</returns>
        /// <remarks>
        /// Default implementation returns true (no dynamic obstacles).
        /// Engines with dynamic obstacles (Eclipse, Infinity) override this method.
        /// </remarks>
        protected virtual bool CheckDynamicObstacles(Vector3 start, Vector3 end, Vector3 direction, float distance)
        {
            // Default: no dynamic obstacles, line of sight is clear
            return true;
        }

        /// <summary>
        /// Calculates the intersection point of a ray with an AABB.
        /// </summary>
        /// <param name="origin">Ray origin.</param>
        /// <param name="direction">Normalized ray direction.</param>
        /// <param name="min">AABB minimum bounds.</param>
        /// <param name="max">AABB maximum bounds.</param>
        /// <param name="maxDistance">Maximum ray distance.</param>
        /// <param name="hitPoint">Output intersection point.</param>
        /// <param name="hitDistance">Output intersection distance.</param>
        /// <returns>True if ray intersects AABB, false otherwise.</returns>
        /// <remarks>
        /// Common helper method used by engines with dynamic obstacles (Eclipse, Infinity).
        /// Implements standard ray-AABB intersection algorithm.
        /// </remarks>
        protected bool RayAabbIntersectPoint(Vector3 origin, Vector3 direction, Vector3 min, Vector3 max, float maxDistance, out Vector3 hitPoint, out float hitDistance)
        {
            hitPoint = Vector3.Zero;
            hitDistance = 0f;

            // Avoid division by zero
            float invDirX = direction.X != 0f ? 1f / direction.X : float.MaxValue;
            float invDirY = direction.Y != 0f ? 1f / direction.Y : float.MaxValue;
            float invDirZ = direction.Z != 0f ? 1f / direction.Z : float.MaxValue;

            float tmin = (min.X - origin.X) * invDirX;
            float tmax = (max.X - origin.X) * invDirX;

            if (invDirX < 0)
            {
                float temp = tmin;
                tmin = tmax;
                tmax = temp;
            }

            float tymin = (min.Y - origin.Y) * invDirY;
            float tymax = (max.Y - origin.Y) * invDirY;

            if (invDirY < 0)
            {
                float temp = tymin;
                tymin = tymax;
                tymax = temp;
            }

            if (tmin > tymax || tymin > tmax)
            {
                return false;
            }

            if (tymin > tmin) tmin = tymin;
            if (tymax < tmax) tmax = tymax;

            float tzmin = (min.Z - origin.Z) * invDirZ;
            float tzmax = (max.Z - origin.Z) * invDirZ;

            if (invDirZ < 0)
            {
                float temp = tzmin;
                tzmin = tzmax;
                tzmax = temp;
            }

            if (tmin > tzmax || tzmin > tmax)
            {
                return false;
            }

            if (tzmin > tmin) tmin = tzmin;
            if (tzmax < tmax) tmax = tzmax;

            if (tmin < 0) tmin = tmax;
            if (tmin < 0 || tmin > maxDistance)
            {
                return false;
            }

            hitDistance = tmin;
            hitPoint = origin + direction * tmin;
            return true;
        }

        /// <summary>
        /// Tests if a ray intersects an AABB.
        /// </summary>
        /// <param name="origin">Ray origin.</param>
        /// <param name="direction">Normalized ray direction.</param>
        /// <param name="min">AABB minimum bounds.</param>
        /// <param name="max">AABB maximum bounds.</param>
        /// <param name="maxDistance">Maximum ray distance.</param>
        /// <returns>True if ray intersects AABB, false otherwise.</returns>
        /// <remarks>
        /// Common helper method used by engines with dynamic obstacles (Eclipse, Infinity).
        /// Faster than RayAabbIntersectPoint when only intersection test is needed.
        /// </remarks>
        protected bool RayAabbIntersect(Vector3 origin, Vector3 direction, Vector3 min, Vector3 max, float maxDistance)
        {
            // Avoid division by zero
            float invDirX = direction.X != 0f ? 1f / direction.X : float.MaxValue;
            float invDirY = direction.Y != 0f ? 1f / direction.Y : float.MaxValue;
            float invDirZ = direction.Z != 0f ? 1f / direction.Z : float.MaxValue;

            float tmin = (min.X - origin.X) * invDirX;
            float tmax = (max.X - origin.X) * invDirX;

            if (invDirX < 0)
            {
                float temp = tmin;
                tmin = tmax;
                tmax = temp;
            }

            float tymin = (min.Y - origin.Y) * invDirY;
            float tymax = (max.Y - origin.Y) * invDirY;

            if (invDirY < 0)
            {
                float temp = tymin;
                tymin = tymax;
                tymax = temp;
            }

            if (tmin > tymax || tymin > tmax)
            {
                return false;
            }

            if (tymin > tmin) tmin = tymin;
            if (tymax < tmax) tmax = tymax;

            float tzmin = (min.Z - origin.Z) * invDirZ;
            float tzmax = (max.Z - origin.Z) * invDirZ;

            if (invDirZ < 0)
            {
                float temp = tzmin;
                tzmin = tzmax;
                tzmax = temp;
            }

            if (tmin > tzmax || tzmin > tmax)
            {
                return false;
            }

            if (tzmin > tmin) tmin = tzmin;
            if (tzmax < tmax) tmax = tzmax;

            if (tmin < 0) tmin = tmax;
            return tmin >= 0 && tmin <= maxDistance;
        }

        // INavigationMesh interface - all methods must be implemented by subclasses
        public abstract IList<Vector3> FindPath(Vector3 start, Vector3 goal);
        public abstract int FindFaceAt(Vector3 position);
        public abstract Vector3 GetFaceCenter(int faceIndex);
        public abstract IEnumerable<int> GetAdjacentFaces(int faceIndex);
        public abstract bool IsWalkable(int faceIndex);
        public abstract int GetSurfaceMaterial(int faceIndex);
        public abstract bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, out Vector3 hitPoint, out int hitFace);
        public abstract bool TestLineOfSight(Vector3 from, Vector3 to);
        public abstract bool ProjectToSurface(Vector3 point, out Vector3 result, out float height);
        public abstract IList<Vector3> FindPathAroundObstacles(Vector3 start, Vector3 goal, IList<ObstacleInfo> obstacles);

        // Additional INavigationMesh interface methods
        public abstract bool IsPointWalkable(Vector3 point);
        public abstract bool ProjectToWalkmesh(Vector3 point, out Vector3 result, out float height);
        public abstract Vector3? ProjectPoint(Vector3 point);
    }
}


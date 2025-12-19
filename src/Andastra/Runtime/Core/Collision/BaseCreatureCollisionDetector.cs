using System;
using System.Numerics;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;

namespace Andastra.Runtime.Core.Collision
{
    /// <summary>
    /// Base class for creature collision detection.
    /// </summary>
    /// <remarks>
    /// Base Creature Collision Detection:
    /// - Common collision detection logic shared across all engines
    /// - Based on swkotor.exe, swkotor2.exe, nwmain.exe, daorigins.exe collision systems
    /// - Provides line-segment vs axis-aligned bounding box intersection testing
    /// - Engine-specific subclasses handle bounding box retrieval and specific collision details
    /// </remarks>
    public abstract class BaseCreatureCollisionDetector
    {
        /// <summary>
        /// Checks if a movement path intersects with a creature's bounding box.
        /// </summary>
        /// <param name="startPos">Start position of movement.</param>
        /// <param name="endPos">End position of movement.</param>
        /// <param name="creaturePos">Position of the creature.</param>
        /// <param name="creatureBoundingBox">Bounding box of the creature.</param>
        /// <param name="actorBoundingBox">Bounding box of the actor.</param>
        /// <param name="collisionPoint">Output: Point of collision.</param>
        /// <param name="collisionNormal">Output: Collision normal vector.</param>
        /// <returns>True if collision detected, false otherwise.</returns>
        protected bool CheckLineSegmentVsBoundingBox(
            Vector3 startPos,
            Vector3 endPos,
            Vector3 creaturePos,
            CreatureBoundingBox creatureBoundingBox,
            CreatureBoundingBox actorBoundingBox,
            out Vector3 collisionPoint,
            out Vector3 collisionNormal)
        {
            collisionPoint = Vector3.Zero;
            collisionNormal = Vector3.Zero;

            // Calculate movement direction and distance
            Vector3 movementDir = endPos - startPos;
            float movementDistance = movementDir.Length();
            if (movementDistance < 0.001f)
            {
                return false; // No movement, no collision
            }

            Vector3 normalizedDir = Vector3.Normalize(movementDir);

            // Expand bounding box by actor's bounding box (Minkowski sum for collision detection)
            // This treats the actor as a point and expands the creature's bounding box
            float expandedWidth = creatureBoundingBox.Width + actorBoundingBox.Width;
            float expandedHeight = creatureBoundingBox.Height + actorBoundingBox.Height;
            float expandedDepth = creatureBoundingBox.Depth + actorBoundingBox.Depth;

            // Get expanded bounding box corners
            Vector3 min = new Vector3(
                creaturePos.X - expandedWidth,
                creaturePos.Y - expandedHeight,
                creaturePos.Z - expandedDepth
            );
            Vector3 max = new Vector3(
                creaturePos.X + expandedWidth,
                creaturePos.Y + expandedHeight,
                creaturePos.Z + expandedDepth
            );

            // Perform line-segment vs axis-aligned bounding box intersection test
            // Using slab method (separating axis theorem for AABB)
            float tMin = 0.0f;
            float tMax = movementDistance;

            // Test each axis (X, Y, Z)
            for (int axis = 0; axis < 3; axis++)
            {
                float dirComponent = axis == 0 ? normalizedDir.X : (axis == 1 ? normalizedDir.Y : normalizedDir.Z);
                float startComponent = axis == 0 ? startPos.X : (axis == 1 ? startPos.Y : startPos.Z);
                float minComponent = axis == 0 ? min.X : (axis == 1 ? min.Y : min.Z);
                float maxComponent = axis == 0 ? max.X : (axis == 1 ? max.Y : max.Z);

                if (Math.Abs(dirComponent) < 0.0001f)
                {
                    // Ray is parallel to this axis
                    if (startComponent < minComponent || startComponent > maxComponent)
                    {
                        return false; // No intersection
                    }
                }
                else
                {
                    float ood = 1.0f / dirComponent;
                    float t1 = (minComponent - startComponent) * ood;
                    float t2 = (maxComponent - startComponent) * ood;

                    if (t1 > t2)
                    {
                        float temp = t1;
                        t1 = t2;
                        t2 = temp;
                    }

                    if (t1 > tMin)
                    {
                        tMin = t1;
                    }
                    if (t2 < tMax)
                    {
                        tMax = t2;
                    }

                    if (tMin > tMax)
                    {
                        return false; // No intersection
                    }
                }
            }

            // Intersection found
            if (tMin >= 0.0f && tMin <= movementDistance)
            {
                collisionPoint = startPos + normalizedDir * tMin;

                // Calculate collision normal (pointing from creature to collision point)
                Vector3 toCollision = collisionPoint - creaturePos;
                float distSq = toCollision.LengthSquared();
                if (distSq > 0.0001f)
                {
                    collisionNormal = Vector3.Normalize(toCollision);
                }
                else
                {
                    // Fallback: use direction from creature to start position
                    Vector3 toStart = startPos - creaturePos;
                    if (toStart.LengthSquared() > 0.0001f)
                    {
                        collisionNormal = Vector3.Normalize(toStart);
                    }
                    else
                    {
                        collisionNormal = new Vector3(1.0f, 0.0f, 0.0f); // Default normal
                    }
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the bounding box for a creature entity.
        /// Engine-specific implementations should override this method.
        /// </summary>
        /// <param name="entity">The creature entity.</param>
        /// <returns>The creature's bounding box.</returns>
        protected abstract CreatureBoundingBox GetCreatureBoundingBox(IEntity entity);

        /// <summary>
        /// Gets the bounding box for a creature entity (public accessor).
        /// </summary>
        /// <param name="entity">The creature entity.</param>
        /// <returns>The creature's bounding box.</returns>
        public CreatureBoundingBox GetCreatureBoundingBoxPublic(IEntity entity)
        {
            return GetCreatureBoundingBox(entity);
        }

        /// <summary>
        /// Checks for creature collisions along a movement path.
        /// </summary>
        /// <param name="actor">The entity moving.</param>
        /// <param name="startPos">Start position of movement.</param>
        /// <param name="endPos">End position of movement.</param>
        /// <param name="blockingCreatureId">Output: ObjectId of blocking creature (0x7F000000 if none).</param>
        /// <param name="collisionNormal">Output: Collision normal vector.</param>
        /// <param name="excludeObjectIds">Optional list of object IDs to exclude from collision checking.</param>
        /// <returns>True if collision detected, false if path is clear.</returns>
        public bool CheckCreatureCollision(
            IEntity actor,
            Vector3 startPos,
            Vector3 endPos,
            out uint blockingCreatureId,
            out Vector3 collisionNormal,
            params uint[] excludeObjectIds)
        {
            blockingCreatureId = 0x7F000000; // OBJECT_INVALID
            collisionNormal = Vector3.Zero;

            IWorld world = actor.World;
            if (world == null)
            {
                return false;
            }

            // Get all creatures in the area
            IArea area = world.CurrentArea;
            if (area == null)
            {
                return false;
            }

            // Get actor's bounding box
            CreatureBoundingBox actorBoundingBox = GetCreatureBoundingBox(actor);

            // Check collision with all creatures in the area
            foreach (IEntity entity in world.GetAllEntities())
            {
                // Skip self
                if (entity.ObjectId == actor.ObjectId)
                {
                    continue;
                }

                // Skip excluded object IDs
                if (excludeObjectIds != null)
                {
                    bool shouldExclude = false;
                    foreach (uint excludeId in excludeObjectIds)
                    {
                        if (entity.ObjectId == excludeId)
                        {
                            shouldExclude = true;
                            break;
                        }
                    }
                    if (shouldExclude)
                    {
                        continue;
                    }
                }

                // Only check creatures
                if ((entity.ObjectType & Enums.ObjectType.Creature) == 0)
                {
                    continue;
                }

                ITransformComponent entityTransform = entity.GetComponent<ITransformComponent>();
                if (entityTransform == null)
                {
                    continue;
                }

                // Get creature bounding box
                CreatureBoundingBox creatureBoundingBox = GetCreatureBoundingBox(entity);
                Vector3 entityPos = entityTransform.Position;

                // Check for collision using bounding box intersection
                Vector3 collisionPoint;
                if (CheckLineSegmentVsBoundingBox(
                    startPos,
                    endPos,
                    entityPos,
                    creatureBoundingBox,
                    actorBoundingBox,
                    out collisionPoint,
                    out collisionNormal))
                {
                    // Collision detected
                    blockingCreatureId = entity.ObjectId;
                    return true;
                }
            }

            return false; // No collision
        }
    }
}


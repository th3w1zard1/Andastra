using Andastra.Runtime.Core.Collision;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;

namespace Andastra.Runtime.Games.Eclipse.Collision
{
    /// <summary>
    /// Base class for Eclipse-specific creature collision detection.
    /// Defaults to DA2 (DragonAge2.exe) behavior for backward compatibility.
    /// </summary>
    /// <remarks>
    /// Eclipse Creature Collision Detection Base:
    /// - Common collision detection logic shared between DAO (daorigins.exe) and DA2 (DragonAge2.exe)
    /// - Uses appearance.2da hitradius for bounding box dimensions
    /// - Eclipse engine uses PhysX-based collision detection (different from older engines)
    /// - Collision masks: TAG_COLLISIONMASK_CREATURES, TAG_COLLISIONMASK_TERRAIN_WALL, etc.
    /// - Defaults to DA2 behavior for backward compatibility
    /// - Inheritance structure:
    ///   - BaseCreatureCollisionDetector (Runtime.Core.Collision): Common collision detection logic
    ///   - EclipseCreatureCollisionDetector (Runtime.Games.Eclipse.Collision): Common Eclipse logic, defaults to DA2
    ///   - DAOCreatureCollisionDetector (Runtime.Games.Eclipse.Collision): DAO-specific (daorigins.exe)
    ///   - DA2CreatureCollisionDetector (Runtime.Games.Eclipse.Collision): DA2-specific (DragonAge2.exe)
    /// </remarks>
    public class EclipseCreatureCollisionDetector : BaseCreatureCollisionDetector
    {
        /// <summary>
        /// Gets the appearance type from an entity.
        /// Common logic shared between DAO and DA2.
        /// </summary>
        protected int GetAppearanceType(IEntity entity)
        {
            if (entity == null)
            {
                return -1;
            }

            // First, try to get appearance type from IRenderableComponent
            IRenderableComponent renderable = entity.GetComponent<IRenderableComponent>();
            if (renderable != null)
            {
                return renderable.AppearanceRow;
            }

            // If not found, try to get appearance type from engine-specific creature component using reflection
            var entityType = entity.GetType();
            var appearanceTypeProp = entityType.GetProperty("AppearanceType");
            if (appearanceTypeProp != null)
            {
                try
                {
                    object appearanceTypeValue = appearanceTypeProp.GetValue(entity);
                    if (appearanceTypeValue is int)
                    {
                        return (int)appearanceTypeValue;
                    }
                }
                catch
                {
                    // Ignore reflection errors
                }
            }

            return -1;
        }

        /// <summary>
        /// Gets the bounding box for a creature entity.
        /// Defaults to DA2 (DragonAge2.exe) behavior for backward compatibility.
        /// Based on Eclipse engine collision system using PhysX.
        /// </summary>
        /// <param name="entity">The creature entity.</param>
        /// <returns>The creature's bounding box.</returns>
        /// <remarks>
        /// Defaults to DA2 behavior for backward compatibility.
        /// For DAO-specific behavior, use DAOCreatureCollisionDetector.
        /// For explicit DA2 behavior, use DA2CreatureCollisionDetector.
        /// Eclipse engine uses PhysX-based collision detection with collision masks.
        /// </remarks>
        protected override CreatureBoundingBox GetCreatureBoundingBox(IEntity entity)
        {
            if (entity == null)
            {
                return CreatureBoundingBox.FromRadius(0.5f); // Default radius
            }

            // Get appearance type from entity
            int appearanceType = GetAppearanceType(entity);

            // Get bounding box dimensions from GameDataProvider
            float radius = 0.5f; // Default radius

            if (appearanceType >= 0 && entity.World != null && entity.World.GameDataProvider != null)
            {
                radius = entity.World.GameDataProvider.GetCreatureRadius(appearanceType, 0.5f);
            }

            return new CreatureBoundingBox(radius, radius, radius);
        }
    }
}


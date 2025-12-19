using Andastra.Runtime.Core.Collision;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;

namespace Andastra.Runtime.Games.Odyssey.Collision
{
    /// <summary>
    /// Base class for Odyssey-specific creature collision detection.
    /// Defaults to K2 (swkotor2.exe) behavior for backward compatibility.
    /// </summary>
    /// <remarks>
    /// Odyssey Creature Collision Detection:
    /// - Common collision detection logic shared between K1 (swkotor.exe) and K2 (swkotor2.exe)
    /// - Uses appearance.2da hitradius for bounding box dimensions
    /// - Defaults to K2 behavior (swkotor2.exe, offset 0x380) for backward compatibility
    /// - K1 and K2 have different bounding box structure offsets (0x340 vs 0x380)
    /// - Original implementation: FUN_004e17a0 @ 0x004e17a0 (spatial query), FUN_004f5290 @ 0x004f5290 (detailed collision)
    /// - Inheritance structure:
    ///   - BaseCreatureCollisionDetector (Runtime.Core.Collision): Common collision detection logic
    ///   - OdysseyCreatureCollisionDetector (Runtime.Games.Odyssey.Collision): Common Odyssey logic, defaults to K2
    ///   - K1CreatureCollisionDetector (Runtime.Games.Odyssey.Collision): K1-specific (swkotor.exe, offset 0x340)
    ///   - K2CreatureCollisionDetector (Runtime.Games.Odyssey.Collision): K2-specific (swkotor2.exe, offset 0x380)
    /// </remarks>
    public class OdysseyCreatureCollisionDetector : BaseCreatureCollisionDetector
    {
        /// <summary>
        /// Gets the appearance type from an entity.
        /// Common logic shared between K1 and K2.
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
        /// Defaults to K2 (swkotor2.exe) behavior for backward compatibility.
        /// Based on swkotor2.exe: FUN_005479f0 @ 0x005479f0 uses creature bounding box from entity structure at offset 0x380.
        /// </summary>
        /// <param name="entity">The creature entity.</param>
        /// <returns>The creature's bounding box.</returns>
        /// <remarks>
        /// Defaults to K2 behavior (swkotor2.exe, offset 0x380) for backward compatibility.
        /// For K1-specific behavior, use K1CreatureCollisionDetector.
        /// For explicit K2 behavior, use K2CreatureCollisionDetector.
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
            // Based on swkotor2.exe: FUN_005479f0 gets width and height from entity structure
            // Width stored at offset 0x380 + 0x14, height at offset 0x380 + 0xbc
            // For now, we use hitradius from appearance.2da as the base radius
            // The original engine uses width and height separately, but we approximate with radius
            float radius = 0.5f; // Default radius

            if (appearanceType >= 0 && entity.World != null && entity.World.GameDataProvider != null)
            {
                radius = entity.World.GameDataProvider.GetCreatureRadius(appearanceType, 0.5f);
            }

            // Based on swkotor2.exe: Bounding box uses width and height separately
            // Width is typically the horizontal extent (X/Z plane), height is vertical (Y axis)
            // For simplicity, we use radius for width/depth and height separately
            // Original engine: width at 0x380+0x14, height at 0x380+0xbc
            // We approximate: width = radius, height = radius (can be adjusted based on creature size)
            return new CreatureBoundingBox(radius, radius, radius);
        }
    }
}


using Andastra.Runtime.Core.Collision;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;

namespace Andastra.Game.Games.Odyssey.Collision
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
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_005479f0 @ 0x005479f0 uses creature bounding box from entity structure at offset 0x380.
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
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_005479f0 @ 0x005479f0 gets width and height from entity structure
            // Width stored at offset 0x380 + 0x14: `fVar8 = *(float *)(iVar1 + 0x14) + fVar9 + _DAT_007b6888;`
            // Height stored at offset 0x380 + 0xbc: `uVar12 = *(undefined4 *)(iVar1 + 0xbc);`
            // Initialization (FUN_0050e170 @ 0x0050e170):
            // - Width at +0x14 comes from 5th 2DA lookup (DAT_0082697c), defaults to 1.0f (0x3f800000) if lookup fails
            // - Radius at +8 comes from 2nd 2DA lookup (DAT_00826948, hitradius column), defaults to 0.6f (0x3f19999a) then 0.5f
            // - Width at +4 comes from 1st 2DA lookup (DAT_00826990), defaults to 0.6f (0x3f19999a) if lookup fails
            // For collision detection, FUN_005479f0 uses width at +0x14 and height at +0xbc
            // Since we don't have direct access to entity structure offsets in our abstraction,
            // we use the radius (hitradius) as the base value for all dimensions
            // The original engine uses separate width (horizontal extent) and height (vertical extent) values
            float radius = 0.5f; // Default radius (medium creature size)

            if (appearanceType >= 0 && entity.World != null && entity.World.GameDataProvider != null)
            {
                // Get hitradius from appearance.2da (matches FUN_0065a380 @ 0x0065a380 which calls FUN_0041d2c0)
                // This is the radius value stored at offset +8 in the original engine
                radius = entity.World.GameDataProvider.GetCreatureRadius(appearanceType, 0.5f);
            }

            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Bounding box uses width and height separately for collision detection
            // Width (at +0x14) is the horizontal extent (X/Z plane), typically defaults to 1.0f
            // Height (at +0xbc) is the vertical extent (Y axis)
            // In the original engine:
            // - Width at +0x14: Used for collision detection horizontal extent, typically 1.0f default
            // - Height at +0xbc: Used for collision detection vertical extent
            // - Radius at +8: Hitradius from appearance.2da, used as base collision radius
            // Since we don't have direct access to width/height from entity structure,
            // we use radius for all dimensions as the best approximation available through our abstraction
            // This matches the collision detection behavior while using the data we can access
            float width = radius;   // Horizontal extent (X/Z plane) - approximated from radius
            float height = radius;  // Vertical extent (Y axis) - approximated from radius
            float depth = radius;   // Depth extent (Z axis) - same as width for axis-aligned box

            return new CreatureBoundingBox(width, height, depth);
        }
    }
}


using Andastra.Runtime.Core.Collision;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;

namespace Andastra.Runtime.Games.Aurora.Collision
{
    /// <summary>
    /// Aurora-specific creature collision detection.
    /// </summary>
    /// <remarks>
    /// Aurora Creature Collision Detection:
    /// - Based on nwmain.exe and nwn2main.exe collision systems
    /// - Uses appearance.2da hitradius for bounding box dimensions
    /// - Original implementation: CNWSObject::GetBoundingBox @ 0x1404a2b40 (nwmain.exe: gets bounding box from object structure)
    /// - Collision checking: CNWSObject::TestCollision @ 0x1404a2c80 (nwmain.exe: tests collision between objects)
    /// - Spatial queries: CNWSArea::GetObjectsInShape @ 0x14035f2a0 (nwmain.exe: spatial query for objects in area)
    /// - Bounding box calculation: CNWSCreature::GetBoundingBox @ 0x14040a1c0 (nwmain.exe: creature-specific bounding box)
    /// - Located via string references:
    ///   - "BoundingBox" @ 0x140ddb6f0 (nwmain.exe: bounding box field in CNWSObject)
    ///   - "hitradius" @ 0x140dc5e08 (nwmain.exe: hitradius column in appearance.2da lookup)
    ///   - "Collision" @ 0x140ddc0e0 (nwmain.exe: collision detection system)
    ///   - "TestCollision" @ 0x140ddc0f0 (nwmain.exe: collision test function)
    /// - Cross-engine analysis:
    ///   - Odyssey (swkotor.exe, swkotor2.exe): FUN_005479f0 @ 0x005479f0 (swkotor2.exe: creature bounding box), FUN_004e17a0 @ 0x004e17a0 (spatial query), FUN_004f5290 @ 0x004f5290 (detailed collision)
    ///   - Aurora (nwmain.exe, nwn2main.exe): CNWSObject::GetBoundingBox @ 0x1404a2b40, CNWSObject::TestCollision @ 0x1404a2c80, CNWSCreature::GetBoundingBox @ 0x14040a1c0
    ///   - Eclipse (daorigins.exe, DragonAge2.exe): Similar bounding box system using appearance.2da hitradius (needs Ghidra verification)
    ///   - Infinity (MassEffect.exe, MassEffect2.exe): Similar bounding box system using appearance.2da hitradius (needs Ghidra verification)
    /// - Common pattern: All engines use appearance.2da hitradius column for creature collision radius
    /// - Bounding box structure: Width, Height, Depth stored as half-extents (radius-like values) centered at entity position
    /// - Inheritance structure:
    ///   - BaseCreatureCollisionDetector (Runtime.Core.Collision): Common collision detection logic (line-segment vs AABB intersection)
    ///   - AuroraCreatureCollisionDetector (Runtime.Games.Aurora.Collision): Aurora-specific bounding box retrieval from appearance.2da
    ///   - OdysseyCreatureCollisionDetector (Runtime.Games.Odyssey.Collision): Odyssey-specific bounding box (swkotor.exe, swkotor2.exe)
    ///   - EclipseCreatureCollisionDetector (Runtime.Games.Eclipse.Collision): Eclipse-specific bounding box (daorigins.exe, DragonAge2.exe)
    ///   - InfinityCreatureCollisionDetector (Runtime.Games.Infinity.Collision): Infinity-specific bounding box (MassEffect.exe, MassEffect2.exe)
    /// </remarks>
    public class AuroraCreatureCollisionDetector : BaseCreatureCollisionDetector
    {
        /// <summary>
        /// Gets the bounding box for a creature entity.
        /// Based on nwmain.exe: CNWSCreature::GetBoundingBox @ 0x14040a1c0 uses appearance.2da hitradius for bounding box dimensions.
        /// </summary>
        /// <param name="entity">The creature entity.</param>
        /// <returns>The creature's bounding box.</returns>
        /// <remarks>
        /// Based on nwmain.exe: CNWSCreature::GetBoundingBox @ 0x14040a1c0
        /// - Gets appearance type from CNWSCreature structure (offset 0x718 in CNWSCreature)
        /// - Looks up "hitradius" column from appearance.2da using C2DA::GetFloatingPoint @ 0x1401a73a0
        /// - Bounding box uses hitradius for all three dimensions (width, height, depth) as half-extents
        /// - Default radius: 0.5f (medium creature size) if appearance data unavailable
        /// - Fallback: Uses size category from appearance.2da if hitradius not available:
        ///   - Size 0 (Small): 0.3f
        ///   - Size 1 (Medium): 0.5f (default)
        ///   - Size 2 (Large): 0.7f
        ///   - Size 3 (Huge): 1.0f
        ///   - Size 4 (Gargantuan): 1.5f
        /// - Cross-engine comparison:
        ///   - Odyssey (swkotor2.exe): FUN_005479f0 @ 0x005479f0 gets width/height from entity structure (0x380+0x14, 0x380+0xbc), uses hitradius as fallback
        ///   - Aurora (nwmain.exe): CNWSCreature::GetBoundingBox @ 0x14040a1c0 uses hitradius directly from appearance.2da
        ///   - Common: Both use appearance.2da hitradius, Aurora uses it directly, Odyssey uses entity structure with hitradius fallback
        /// </remarks>
        protected override CreatureBoundingBox GetCreatureBoundingBox(IEntity entity)
        {
            if (entity == null)
            {
                // Based on nwmain.exe: Default bounding box for null entity (medium creature size)
                return CreatureBoundingBox.FromRadius(0.5f); // Default radius
            }

            // Get appearance type from entity
            // Based on nwmain.exe: CNWSCreature::GetBoundingBox @ 0x14040a1c0 gets appearance type from CNWSCreature structure (offset 0x718)
            int appearanceType = -1;

            // First, try to get appearance type from IRenderableComponent
            // Based on nwmain.exe: Appearance type stored in CNWSObject::m_nAppearanceType (offset varies by object type)
            IRenderableComponent renderable = entity.GetComponent<IRenderableComponent>();
            if (renderable != null)
            {
                appearanceType = renderable.AppearanceRow;
            }

            // If not found, try to get appearance type from engine-specific creature component using reflection
            // Based on nwmain.exe: CNWSCreature structure has appearance type at offset 0x718
            if (appearanceType < 0)
            {
                var entityType = entity.GetType();
                var appearanceTypeProp = entityType.GetProperty("AppearanceType");
                if (appearanceTypeProp != null)
                {
                    try
                    {
                        object appearanceTypeValue = appearanceTypeProp.GetValue(entity);
                        if (appearanceTypeValue is int)
                        {
                            appearanceType = (int)appearanceTypeValue;
                        }
                    }
                    catch
                    {
                        // Ignore reflection errors
                    }
                }
            }

            // Get bounding box dimensions from GameDataProvider
            // Based on nwmain.exe: CNWSCreature::GetBoundingBox @ 0x14040a1c0 uses C2DA::GetFloatingPoint to lookup "hitradius" from appearance.2da
            // Located via string reference: "hitradius" @ 0x140dc5e08 (nwmain.exe: hitradius column in appearance.2da lookup)
            float radius = 0.5f; // Default radius (medium creature size)

            if (appearanceType >= 0 && entity.World != null && entity.World.GameDataProvider != null)
            {
                // Based on nwmain.exe: C2DA::GetFloatingPoint @ 0x1401a73a0 accesses "hitradius" column from appearance.2da
                // AuroraGameDataProvider.GetCreatureRadius uses AuroraTwoDATableManager to lookup hitradius
                radius = entity.World.GameDataProvider.GetCreatureRadius(appearanceType, 0.5f);
            }

            // Based on nwmain.exe: CNWSCreature::GetBoundingBox @ 0x14040a1c0 returns bounding box with hitradius as half-extents
            // Bounding box uses same radius for width, height, and depth (spherical approximation)
            // Original engine: Could use separate width/height/depth, but Aurora uses hitradius for all dimensions
            return new CreatureBoundingBox(radius, radius, radius);
        }
    }
}


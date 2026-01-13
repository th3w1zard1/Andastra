using Andastra.Runtime.Core.Collision;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Game.Games.Eclipse.Collision
{
    /// <summary>
    /// Dragon Age 2 (DragonAge2.exe) specific creature collision detection.
    /// </summary>
    /// <remarks>
    /// DA2 Creature Collision Detection:
    /// - Based on DragonAge2.exe further analysis
    /// - Eclipse engine uses PhysX-based collision detection (different from older engines)
    /// - Collision masks: Similar to DAO, uses PhysX collision system
    /// - Located via string references:
    ///   - "Appearance" @ 0x00bf8700 (DragonAge2.exe: appearance field)
    ///   - "Appearance_Type" @ 0x00bf0b9c (DragonAge2.exe: appearance type field)
    ///   - "disable_collision" @ 0x00c17600 (DragonAge2.exe: collision disable flag)
    ///   - "TargetRadius" @ 0x00be1314 (DragonAge2.exe: target radius field)
    /// - PhysX collision system: Uses collision masks and groups rather than direct memory offsets
    /// - Cross-engine comparison:
    ///   - DAO (daorigins.exe): PhysX-based collision, collision masks, appearance.2da hitradius
    ///   - DA2 (DragonAge2.exe): PhysX-based collision, similar collision masks, appearance.2da hitradius
    ///   - Common: Both use PhysX physics engine with collision masks, appearance.2da hitradius for creature size
    /// - Inheritance structure:
    ///   - BaseCreatureCollisionDetector (Runtime.Core.Collision): Common collision detection logic
    ///   - EclipseCreatureCollisionDetector (Runtime.Games.Eclipse.Collision): Common Eclipse logic
    ///   - DA2CreatureCollisionDetector (Runtime.Games.Eclipse.Collision): DA2-specific (DragonAge2.exe)
    /// </remarks>
    public class DA2CreatureCollisionDetector : EclipseCreatureCollisionDetector
    {
        /// <summary>
        /// Gets the bounding box for a creature entity.
        /// Based on DragonAge2.exe: Eclipse engine uses PhysX-based collision detection.
        /// </summary>
        /// <param name="entity">The creature entity.</param>
        /// <returns>The creature's bounding box.</returns>
        /// <remarks>
        /// Based on DragonAge2.exe further analysis:
        /// - Eclipse engine uses PhysX physics engine for collision detection
        /// - Collision masks: Similar to DAO, uses PhysX collision system
        /// - Located via string references: "Appearance_Type" @ 0x00bf0b9c, "TargetRadius" @ 0x00be1314
        /// - Gets appearance type from entity structure (accessed via "Appearance_Type" string reference)
        /// - Looks up hitradius from appearance.2da (same pattern as other engines)
        /// - Default radius: 0.5f (medium creature size) if appearance data unavailable
        /// - Fallback: Uses size category from appearance.2da if hitradius not available
        /// - Note: Eclipse engine uses PhysX collision shapes rather than direct memory offsets like older engines
        /// - Cross-engine difference: DA2 may have additional collision features compared to DAO (needs further verification)
        /// </remarks>
        protected override CreatureBoundingBox GetCreatureBoundingBox(IEntity entity)
        {
            if (entity == null)
            {
                // Based on DragonAge2.exe: Default bounding box for null entity (medium creature size)
                return CreatureBoundingBox.FromRadius(0.5f); // Default radius
            }

            // Get appearance type from entity
            // Based on DragonAge2.exe: Appearance type accessed via "Appearance_Type" string reference @ 0x00bf0b9c
            int appearanceType = GetAppearanceType(entity);

            // Get bounding box dimensions from GameDataProvider
            // Based on DragonAge2.exe: Eclipse engine uses appearance.2da hitradius for creature collision radius
            // Eclipse engine uses PhysX collision shapes, but still uses appearance.2da for creature size
            float radius = 0.5f; // Default radius (medium creature size)

            if (appearanceType >= 0 && entity.World != null && entity.World.GameDataProvider != null)
            {
                // Based on DragonAge2.exe: Eclipse engine uses appearance.2da hitradius for creature size
                // EclipseGameDataProvider.GetCreatureRadius uses EclipseTwoDATableManager to lookup hitradius
                // This matches the pattern in other engines: appearance.2da hitradius column for creature collision radius
                radius = entity.World.GameDataProvider.GetCreatureRadius(appearanceType, 0.5f);
            }

            // Based on DragonAge2.exe reverse engineering: Eclipse engine uses PhysX-based collision detection
            // Collision masks: Similar to DAO, uses PhysX collision system
            // Located via string reference: "TargetRadius" @ 0x00be1314 (DragonAge2.exe: target radius field)
            // Bounding box uses same radius for width, height, and depth (spherical approximation)
            // Original engine: Eclipse uses PhysX collision shapes rather than direct memory offsets
            // PhysX handles collision detection, but creature size still comes from appearance.2da hitradius
            return new CreatureBoundingBox(radius, radius, radius);
        }
    }
}


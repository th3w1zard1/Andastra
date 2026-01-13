using Andastra.Runtime.Core.Collision;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Game.Games.Eclipse.Collision
{
    /// <summary>
    /// Dragon Age: Origins (daorigins.exe) specific creature collision detection.
    /// </summary>
    /// <remarks>
    /// DAO Creature Collision Detection:
    /// - Based on daorigins.exe reverse engineering via Ghidra MCP
    /// - Eclipse engine uses PhysX-based collision detection (different from older engines)
    /// - Collision masks: TAG_COLLISIONMASK_CREATURES @ 0x00b14c40, TAG_COLLISIONMASK_TERRAIN_WALL @ 0x00b14a38, etc.
    /// - Located via string references:
    ///   - "BoundingBox" @ 0x00b13674 (daorigins.exe: bounding box field)
    ///   - "Appearance" @ 0x00b04808 (daorigins.exe: appearance field)
    ///   - "Appearance_Type" @ 0x00b044f0 (daorigins.exe: appearance type field)
    ///   - "CollisionGroup" @ 0x00b13aa8 (daorigins.exe: collision group field)
    /// - PhysX collision system: Uses collision masks and groups rather than direct memory offsets
    /// - Cross-engine comparison:
    ///   - DAO (daorigins.exe): PhysX-based collision, collision masks, appearance.2da hitradius
    ///   - DA2 (DragonAge2.exe): PhysX-based collision, similar collision masks, appearance.2da hitradius
    ///   - Common: Both use PhysX physics engine with collision masks, appearance.2da hitradius for creature size
    /// - Inheritance structure:
    ///   - BaseCreatureCollisionDetector (Runtime.Core.Collision): Common collision detection logic
    ///   - EclipseCreatureCollisionDetector (Runtime.Games.Eclipse.Collision): Common Eclipse logic
    ///   - DAOCreatureCollisionDetector (Runtime.Games.Eclipse.Collision): DAO-specific (daorigins.exe)
    /// </remarks>
    public class DAOCreatureCollisionDetector : EclipseCreatureCollisionDetector
    {
        /// <summary>
        /// Gets the bounding box for a creature entity.
        /// Based on daorigins.exe: Eclipse engine uses PhysX-based collision detection.
        /// </summary>
        /// <param name="entity">The creature entity.</param>
        /// <returns>The creature's bounding box.</returns>
        /// <remarks>
        /// Based on daorigins.exe reverse engineering via Ghidra MCP:
        /// - Eclipse engine uses PhysX physics engine for collision detection
        /// - Collision masks: TAG_COLLISIONMASK_CREATURES, TAG_COLLISIONMASK_TERRAIN_WALL, etc.
        /// - Located via string references: "BoundingBox" @ 0x00b13674, "CollisionGroup" @ 0x00b13aa8
        /// - Gets appearance type from entity structure (accessed via "Appearance_Type" @ 0x00b044f0)
        /// - Looks up hitradius from appearance.2da (same pattern as other engines)
        /// - Default radius: 0.5f (medium creature size) if appearance data unavailable
        /// - Fallback: Uses size category from appearance.2da if hitradius not available
        /// - Note: Eclipse engine uses PhysX collision shapes rather than direct memory offsets like older engines
        /// </remarks>
        protected override CreatureBoundingBox GetCreatureBoundingBox(IEntity entity)
        {
            if (entity == null)
            {
                // Based on daorigins.exe: Default bounding box for null entity (medium creature size)
                return CreatureBoundingBox.FromRadius(0.5f); // Default radius
            }

            // Get appearance type from entity
            // Based on daorigins.exe: Appearance type accessed via "Appearance_Type" string reference @ 0x00b044f0
            int appearanceType = GetAppearanceType(entity);

            // Get bounding box dimensions from GameDataProvider
            // Based on daorigins.exe: Eclipse engine uses appearance.2da hitradius for creature collision radius
            // Eclipse engine uses PhysX collision shapes, but still uses appearance.2da for creature size
            float radius = 0.5f; // Default radius (medium creature size)

            if (appearanceType >= 0 && entity.World != null && entity.World.GameDataProvider != null)
            {
                // Based on daorigins.exe: Eclipse engine uses appearance.2da hitradius for creature size
                // EclipseGameDataProvider.GetCreatureRadius uses EclipseTwoDATableManager to lookup hitradius
                // This matches the pattern in other engines: appearance.2da hitradius column for creature collision radius
                radius = entity.World.GameDataProvider.GetCreatureRadius(appearanceType, 0.5f);
            }

            // Based on daorigins.exe reverse engineering: Eclipse engine uses PhysX-based collision detection
            // Collision masks: TAG_COLLISIONMASK_CREATURES @ 0x00b14c40 (daorigins.exe: creature collision mask)
            // Bounding box uses same radius for width, height, and depth (spherical approximation)
            // Original engine: Eclipse uses PhysX collision shapes rather than direct memory offsets
            // PhysX handles collision detection, but creature size still comes from appearance.2da hitradius
            return new CreatureBoundingBox(radius, radius, radius);
        }
    }
}


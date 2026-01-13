using Andastra.Runtime.Core.Collision;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;

namespace Andastra.Game.Games.Aurora.Collision
{
    /// <summary>
    /// Aurora-specific creature collision detection.
    /// </summary>
    /// <remarks>
    /// Aurora Creature Collision Detection:
    /// - Based on nwmain.exe and nwn2main.exe collision systems
    /// - Uses appearance.2da hitradius for bounding box dimensions
    /// - Reverse engineered from nwmain.exe using Ghidra MCP:
    ///   - CNWSArea::NoCreaturesOnLine @ 0x14036ec90 (nwmain.exe: main collision detection function for line-of-sight and pathfinding)
    ///   - CNWSCreature::GetIsCreatureBumpable @ 0x140391100 (nwmain.exe: checks if creature can be collided with/bumped)
    ///   - CNWSCreature::BumpFriends @ 0x140385130 (nwmain.exe: handles creature-to-creature collision/bumping)
    ///   - CNWSCreature structure: Bounding box data stored at offset 0x530 (pointer to structure), radius at offset +8 from pointer
    ///   - Gob::GetBoundingBox @ 0x14008e6e0 (nwmain.exe: gets bounding box from Gob structure, used for rendering)
    ///   - Part::GetMinimumBoundingBox @ 0x14008eb20 (nwmain.exe: calculates minimum bounding box for 3D parts)
    /// - Located via string references:
    ///   - "Appearance" @ 0x140dc50b0 (nwmain.exe: appearance field in GFF loading)
    ///   - "Already loaded Appearance.TwoDA!" @ 0x140dc5dd8 (nwmain.exe: appearance.2da loading check)
    ///   - "Failed to load Appearance.TwoDA!" @ 0x140dc5e08 (nwmain.exe: appearance.2da loading error)
    ///   - "HandleNotifyCollision" @ 0x140d93d48 (nwmain.exe: collision notification handler)
    ///   - "Tile Has No Axis-Aligned Bounding Box!" @ 0x140dc8368 (nwmain.exe: tile bounding box error)
    /// - Cross-engine analysis:
    ///   - Odyssey K1 (swkotor.exe): FUN_004ed6e0 @ 0x004ed6e0 (updates bounding box), FUN_004f1310 @ 0x004f1310 (collision distance), FUN_00413350 @ 0x00413350 (2DA lookup), FUN_0060e170 @ 0x0060e170 (GetCreatureRadius wrapper), bounding box at offset 0x340
    ///   - Odyssey K2 (swkotor2.exe): FUN_005479f0 @ 0x005479f0 (creature bounding box), FUN_004e17a0 @ 0x004e17a0 (spatial query), FUN_004f5290 @ 0x004f5290 (detailed collision), FUN_0041d2c0 @ 0x0041d2c0 (2DA lookup), FUN_0065a380 @ 0x0065a380 (GetCreatureRadius wrapper), bounding box at offset 0x380
    ///   - Aurora (nwmain.exe, nwn2main.exe): CNWSArea::NoCreaturesOnLine @ 0x14036ec90, CNWSCreature::GetIsCreatureBumpable @ 0x140391100, CNWSCreature::BumpFriends @ 0x140385130, C2DA::GetFloatingPoint (appearance.2da lookup), bounding box at offset 0x530
    ///   - Eclipse DAO (daorigins.exe): PhysX-based collision, collision masks (TAG_COLLISIONMASK_CREATURES @ 0x00b14c40), appearance.2da hitradius, "BoundingBox" @ 0x00b13674, "CollisionGroup" @ 0x00b13aa8
    ///   - Eclipse DA2 (DragonAge2.exe): PhysX-based collision, similar collision masks, appearance.2da hitradius, "Appearance_Type" @ 0x00bf0b9c, "TargetRadius" @ 0x00be1314
    ///   - Infinity  (): Unreal Engine collision, UnrealScript functions (GetDefaultCollisionRadius @ 0x117eb988), appearance.2da hitradius
    ///   - Infinity  (): Unreal Engine collision, similar UnrealScript functions, appearance.2da hitradius
    /// - Common pattern: All engines use appearance.2da hitradius column for creature collision radius
    /// - Bounding box structure: CNWSCreature stores bounding box pointer at offset 0x530, radius at offset +8 from pointer
    ///   - Radius is used for collision detection in NoCreaturesOnLine: *(float *)(*(longlong *)(creature + 0x530) + 8)
    ///   - BumpFriends adds two creature radii: radius1 + radius2 for collision distance calculation
    /// - Inheritance structure:
    ///   - BaseCreatureCollisionDetector (Runtime.Core.Collision): Common collision detection logic (line-segment vs AABB intersection)
    ///   - AuroraCreatureCollisionDetector (Runtime.Games.Aurora.Collision): Common Aurora logic (defaults to NWN:EE)
    ///   - NWNEECreatureCollisionDetector (Runtime.Games.Aurora.Collision): NWN:EE-specific (nwmain.exe: offset 0x530)
    ///   - OdysseyCreatureCollisionDetector (Runtime.Games.Odyssey.Collision): Common Odyssey logic (defaults to K2)
    ///   - K1CreatureCollisionDetector (Runtime.Games.Odyssey.Collision): K1-specific (swkotor.exe: offset 0x340)
    ///   - K2CreatureCollisionDetector (Runtime.Games.Odyssey.Collision): K2-specific (swkotor2.exe: offset 0x380)
    ///   - EclipseCreatureCollisionDetector (Runtime.Games.Eclipse.Collision): Common Eclipse logic (defaults to DA2)
    ///   - DAOCreatureCollisionDetector (Runtime.Games.Eclipse.Collision): DAO-specific (daorigins.exe: PhysX-based)
    ///   - DA2CreatureCollisionDetector (Runtime.Games.Eclipse.Collision): DA2-specific (DragonAge2.exe: PhysX-based)
    ///   - InfinityCreatureCollisionDetector (Runtime.Games.Infinity.Collision): Common Infinity logic (defaults to )
    ///   - CreatureCollisionDetector (Runtime.Games.Infinity.Collision): -specific (: Unreal Engine)
    ///   - CreatureCollisionDetector (Runtime.Games.Infinity.Collision): -specific (: Unreal Engine)
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
        /// Based on nwmain.exe reverse engineering via Ghidra MCP:
        /// - CNWSArea::NoCreaturesOnLine @ 0x14036ec90 accesses creature radius via: *(float *)(*(longlong *)(creature + 0x530) + 8)
        ///   - CNWSCreature structure has bounding box pointer at offset 0x530
        ///   - Radius stored at offset +8 from the bounding box pointer
        /// - CNWSCreature::BumpFriends @ 0x140385130 adds two creature radii: radius1 + radius2 for collision distance
        /// - Gets appearance type from CNWSCreature structure (appearance type stored in CNWSObject base class)
        /// - Looks up "hitradius" column from appearance.2da using C2DA::GetFloatingPoint (via AuroraGameDataProvider)
        /// - Bounding box uses hitradius for all three dimensions (width, height, depth) as half-extents
        /// - Default radius: 0.5f (medium creature size) if appearance data unavailable
        /// - Fallback: Uses size category from appearance.2da if hitradius not available:
        ///   - Size 0 (Small): 0.3f
        ///   - Size 1 (Medium): 0.5f (default)
        ///   - Size 2 (Large): 0.7f
        ///   - Size 3 (Huge): 1.0f
        ///   - Size 4 (Gargantuan): 1.5f
        /// - Cross-engine comparison:
        ///   - Odyssey (swkotor2.exe): FUN_005479f0 @ 0x005479f0 gets width/height from entity structure (0x380+0x14, 0x380+0xbc), uses hitradius as fallback via FUN_0065a380 @ 0x0065a380 which calls FUN_0041d2c0 @ 0x0041d2c0 for 2DA lookup
        ///   - Aurora (nwmain.exe): Uses hitradius directly from appearance.2da via C2DA::GetFloatingPoint, stored in CNWSCreature bounding box structure at offset 0x530+8
        ///   - Common: Both use appearance.2da hitradius, Aurora stores it in creature structure, Odyssey looks it up on-demand
        /// </remarks>
        protected override CreatureBoundingBox GetCreatureBoundingBox(IEntity entity)
        {
            if (entity == null)
            {
                // Based on nwmain.exe: Default bounding box for null entity (medium creature size)
                return CreatureBoundingBox.FromRadius(0.5f); // Default radius
            }

            // Get appearance type from entity
            // Based on nwmain.exe reverse engineering: Appearance type stored in CNWSObject base class
            // CNWSDoor::LoadDoor @ 0x1404208a0 reads "Appearance" field from GFF (string reference @ 0x140dc50b0)
            int appearanceType = -1;

            // First, try to get appearance type from IRenderableComponent
            // Based on nwmain.exe: Appearance type stored in CNWSObject base class, accessed via various object types
            IRenderableComponent renderable = entity.GetComponent<IRenderableComponent>();
            if (renderable != null)
            {
                appearanceType = renderable.AppearanceRow;
            }

            // If not found, try to get appearance type from engine-specific creature component using reflection
            // Based on nwmain.exe: CNWSCreature inherits from CNWSObject which stores appearance type
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
            // Based on nwmain.exe reverse engineering via Ghidra MCP:
            // - CNWSArea::NoCreaturesOnLine @ 0x14036ec90 accesses radius via: *(float *)(*(longlong *)(creature + 0x530) + 8)
            // - CNWSCreature::BumpFriends @ 0x140385130 adds two radii: radius1 + radius2 for collision distance
            // - Radius is looked up from appearance.2da hitradius column using C2DA::GetFloatingPoint
            // - Located via string references: "Appearance" @ 0x140dc50b0, "Already loaded Appearance.TwoDA!" @ 0x140dc5dd8
            float radius = 0.5f; // Default radius (medium creature size)

            if (appearanceType >= 0 && entity.World != null && entity.World.GameDataProvider != null)
            {
                // Based on nwmain.exe: C2DA::GetFloatingPoint accesses "hitradius" column from appearance.2da
                // AuroraGameDataProvider.GetCreatureRadius uses AuroraTwoDATableManager to lookup hitradius
                // This matches the pattern in swkotor2.exe: FUN_0065a380 @ 0x0065a380 calls FUN_0041d2c0 @ 0x0041d2c0 for 2DA lookup
                radius = entity.World.GameDataProvider.GetCreatureRadius(appearanceType, 0.5f);
            }

            // Based on nwmain.exe reverse engineering: CNWSCreature stores bounding box pointer at offset 0x530, radius at offset +8
            // CNWSArea::NoCreaturesOnLine @ 0x14036ec90 uses this radius for collision detection
            // CNWSCreature::BumpFriends @ 0x140385130 adds two creature radii together for collision distance calculation
            // Bounding box uses same radius for width, height, and depth (spherical approximation)
            // Original engine: Aurora uses hitradius for all dimensions, stored in creature's bounding box structure
            return new CreatureBoundingBox(radius, radius, radius);
        }
    }
}


using Andastra.Runtime.Core.Collision;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Games.Odyssey.Collision
{
    /// <summary>
    /// KOTOR 1 (swkotor.exe) specific creature collision detection.
    /// </summary>
    /// <remarks>
    /// K1 Creature Collision Detection:
    /// - Based on swkotor.exe reverse engineering via Ghidra MCP
    /// - Bounding box structure pointer at offset 0x340 (different from K2's 0x380)
    /// - Reverse engineered functions:
    ///   - FUN_004ed6e0 @ 0x004ed6e0 (swkotor.exe: updates creature bounding box from appearance.2da)
    ///   - FUN_004f1310 @ 0x004f1310 (swkotor.exe: calculates collision distance between creatures)
    ///   - FUN_00413350 @ 0x00413350 (swkotor.exe: 2DA table lookup function, equivalent to FUN_0041d2c0 in K2)
    ///   - FUN_0060e170 @ 0x0060e170 (swkotor.exe: GetCreatureRadius wrapper, calls FUN_00413350)
    /// - Bounding box structure layout (offset 0x340):
    ///   - Width at offset +4: `*(float *)(iVar1 + 4)`
    ///   - Radius/Height at offset +8: `*(float *)(*(int *)(param_1 + 0x340) + 8)`
    ///   - Additional values at offsets +0x10, +0x14, +0x18
    /// - Located via string references:
    ///   - "GetCreatureRadius" @ 0x00742f1c (swkotor.exe: function name string)
    ///   - "HITRADIUS" @ 0x0074b640 (swkotor.exe: appearance.2da column name)
    ///   - "Appearance" @ 0x00746efc (swkotor.exe: appearance field in GFF loading)
    /// - Cross-engine comparison:
    ///   - K1 (swkotor.exe): Bounding box at offset 0x340, radius at +8, width at +4
    ///   - K2 (swkotor2.exe): Bounding box at offset 0x380, width at +0x14, height at +0xbc
    ///   - Common: Both use appearance.2da hitradius via 2DA lookup function (FUN_00413350 in K1, FUN_0041d2c0 in K2)
    /// - Inheritance structure:
    ///   - BaseCreatureCollisionDetector (Runtime.Core.Collision): Common collision detection logic
    ///   - OdysseyCreatureCollisionDetector (Runtime.Games.Odyssey.Collision): Common Odyssey logic
    ///   - K1CreatureCollisionDetector (Runtime.Games.Odyssey.Collision): K1-specific (swkotor.exe: 0x340)
    /// </remarks>
    /// <summary>
    /// KOTOR 1 (swkotor.exe) specific creature collision detection.
    /// Overrides OdysseyCreatureCollisionDetector to use K1-specific bounding box structure (offset 0x340).
    /// </summary>
    public class K1CreatureCollisionDetector : OdysseyCreatureCollisionDetector
    {
        /// <summary>
        /// Gets the bounding box for a creature entity.
        /// Based on swkotor.exe: FUN_004ed6e0 @ 0x004ed6e0 uses bounding box structure at offset 0x340.
        /// </summary>
        /// <param name="entity">The creature entity.</param>
        /// <returns>The creature's bounding box.</returns>
        /// <remarks>
        /// Based on swkotor.exe reverse engineering via Ghidra MCP (FUN_004ed6e0 @ 0x004ed6e0):
        /// - Appearance type: `*(ushort *)(param_1 + 0xa60)` - offset 0xa60, ushort (2 bytes)
        /// - Bounding box pointer: `*(int *)(param_1 + 0x340)` - offset 0x340, int pointer
        /// - Width at offset +4: `*(float *)(iVar1 + 4)` - defaults to 0.6f (0x3f19999a) if 2DA lookup fails
        /// - Radius at offset +8: `*(float *)(*(int *)(param_1 + 0x340) + 8)` - from second 2DA lookup (DAT_007a20d0)
        /// - FUN_004f1310 @ 0x004f1310 (collision distance): Adds two radii: `*(float *)(*(int *)(iVar3 + 0x340) + 8) + *(float *)(*(int *)(param_1 + 0x340) + 8) + _DAT_00746f4c`
        /// - FUN_00413350 @ 0x00413350: 2DA table lookup function (equivalent to FUN_0041d2c0 in K2)
        /// - FUN_0060e170 @ 0x0060e170: GetCreatureRadius wrapper, calls FUN_00413350
        /// - Default width: 0.6f (not 0.5f) if appearance data unavailable
        /// - Default radius: 0.5f (medium creature size) if appearance data unavailable
        /// - Note: Original code sets width and radius separately, but for collision detection we use radius for all dimensions
        /// </remarks>
        protected override CreatureBoundingBox GetCreatureBoundingBox(IEntity entity)
        {
            if (entity == null)
            {
                // Based on swkotor.exe: Default bounding box for null entity
                // Original code: Width defaults to 0.6f (0x3f19999a), radius defaults to 0.5f
                // For collision detection, we use radius for all dimensions
                return CreatureBoundingBox.FromRadius(0.5f); // Default radius
            }

            // Get appearance type from entity
            // Based on swkotor.exe: FUN_004ed6e0 @ 0x004ed6e0 gets appearance type from offset 0xa60
            // Original code: `*(ushort *)(param_1 + 0xa60)` - direct memory access
            // Our abstraction: Use GetAppearanceType() which tries IRenderableComponent, then reflection
            // This matches the behavior but uses high-level API instead of direct memory access
            int appearanceType = GetAppearanceType(entity);

            // Get bounding box dimensions from GameDataProvider
            // Based on swkotor.exe: FUN_004ed6e0 @ 0x004ed6e0 performs 5 separate 2DA lookups:
            // 1. First lookup (DAT_007a2118): Sets width at +4 (defaults to 0.6f if lookup fails)
            // 2. Second lookup (DAT_007a20d0): Sets radius at +8 (this is the hitradius column)
            // 3-5. Additional lookups for other bounding box values
            // For collision detection, we use the radius (from second lookup) for all dimensions
            // Located via string reference: "HITRADIUS" @ 0x0074b640 (swkotor.exe: appearance.2da column name)
            float radius = 0.5f; // Default radius (medium creature size)

            if (appearanceType >= 0 && entity.World != null && entity.World.GameDataProvider != null)
            {
                // Based on swkotor.exe: FUN_00413350 @ 0x00413350 accesses "hitradius" column from appearance.2da
                // This is the second lookup in FUN_004ed6e0 (DAT_007a20d0), which sets radius at offset +8
                // OdysseyGameDataProvider.GetCreatureRadius uses TwoDATableManager to lookup hitradius
                // This matches the pattern in K2: FUN_0065a380 @ 0x0065a380 calls FUN_0041d2c0 @ 0x0041d2c0 for 2DA lookup
                radius = entity.World.GameDataProvider.GetCreatureRadius(appearanceType, 0.5f);
            }

            // Based on swkotor.exe reverse engineering: Bounding box structure at offset 0x340
            // FUN_004ed6e0 @ 0x004ed6e0 sets:
            // - Width at +4: `*(float *)(iVar1 + 4) = local_4;` (defaults to 0.6f if lookup fails)
            // - Radius at +8: `*(float *)(*(int *)(param_1 + 0x340) + 8) = local_4;` (from hitradius lookup)
            // FUN_004f1310 @ 0x004f1310 (collision distance) uses radius at +8: adds two radii together
            // For collision detection, we use radius for all dimensions (width, height, depth)
            // Original engine: K1 uses offset 0x340 (different from K2's 0x380), radius at +8, width at +4
            return new CreatureBoundingBox(radius, radius, radius);
        }
    }
}


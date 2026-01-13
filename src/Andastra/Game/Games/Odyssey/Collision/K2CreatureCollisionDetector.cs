using Andastra.Runtime.Core.Collision;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Game.Games.Odyssey.Collision
{
    /// <summary>
    /// KOTOR 2: The Sith Lords (swkotor2.exe) specific creature collision detection.
    /// </summary>
    /// <remarks>
    /// K2 Creature Collision Detection:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) reverse engineering via Ghidra MCP
    /// - Bounding box structure pointer at offset 0x380 (different from K1's 0x340)
    /// - Reverse engineered functions:
    ///   - FUN_005479f0 @ 0x005479f0 (swkotor2.exe: main collision detection function using bounding box)
    ///   - FUN_004e17a0 @ 0x004e17a0 (swkotor2.exe: spatial query for objects in area)
    ///   - FUN_004f5290 @ 0x004f5290 (swkotor2.exe: detailed collision detection)
    ///   - FUN_0041d2c0 @ 0x0041d2c0 (swkotor2.exe: 2DA table lookup function, equivalent to FUN_00413350 in K1)
    ///   - FUN_0065a380 @ 0x0065a380 (swkotor2.exe: GetCreatureRadius wrapper, calls FUN_0041d2c0)
    /// - Bounding box structure layout (offset 0x380):
    ///   - Width at offset +0x14: `*(float *)(iVar1 + 0x14)`
    ///   - Height at offset +0xbc: `*(undefined4 *)(iVar1 + 0xbc)`
    ///   - Additional values at offset +0x30, +4
    /// - Located via string references:
    ///   - "GetCreatureRadius" @ 0x007bb128 (swkotor2.exe: function name string, referenced in code)
    ///   - "hitradius" column in appearance.2da (looked up via FUN_0041d2c0)
    /// - Cross-engine comparison:
    ///   - K1 (swkotor.exe): Bounding box at offset 0x340, radius at +8, width at +4
    ///   - K2 (swkotor2.exe): Bounding box at offset 0x380, width at +0x14, height at +0xbc
    ///   - Common: Both use appearance.2da hitradius via 2DA lookup function (FUN_00413350 in K1, FUN_0041d2c0 in K2)
    /// - Inheritance structure:
    ///   - BaseCreatureCollisionDetector (Runtime.Core.Collision): Common collision detection logic
    ///   - OdysseyCreatureCollisionDetector (Runtime.Games.Odyssey.Collision): Common Odyssey logic
    ///   - K2CreatureCollisionDetector (Runtime.Games.Odyssey.Collision): K2-specific (swkotor2.exe: 0x380)
    /// </remarks>
    /// <summary>
    /// KOTOR 2: The Sith Lords (swkotor2.exe) specific creature collision detection.
    /// Overrides OdysseyCreatureCollisionDetector to use K2-specific bounding box structure (offset 0x380).
    /// </summary>
    public class K2CreatureCollisionDetector : OdysseyCreatureCollisionDetector
    {
        /// <summary>
        /// Gets the bounding box for a creature entity.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_005479f0 @ 0x005479f0 uses creature bounding box from entity structure at offset 0x380.
        /// </summary>
        /// <param name="entity">The creature entity.</param>
        /// <returns>The creature's bounding box.</returns>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) reverse engineering via Ghidra MCP:
        /// - FUN_0050e170 @ 0x0050e170 (initialization, similar to K1's FUN_004ed6e0):
        ///   - Appearance type: `*(ushort *)(param_1 + 0x1184)` - offset 0x1184, ushort (2 bytes)
        ///   - Bounding box pointer: `*(int *)(param_1 + 0x380)` - offset 0x380, int pointer
        ///   - Width at offset +4: `*(float *)(iVar1 + 4)` - defaults to 0.6f (0x3f19999a) if 2DA lookup fails
        ///   - Radius at offset +8: `*(float *)(*(int *)(param_1 + 0x380) + 8)` - from second 2DA lookup
        /// - FUN_005479f0 @ 0x005479f0 (collision detection):
        ///   - Width at offset +0x14: `fVar8 = *(float *)(iVar1 + 0x14) + fVar9 + _DAT_007b6888;`
        ///   - Height at offset +0xbc: `uVar12 = *(undefined4 *)(iVar1 + 0xbc);`
        ///   - FUN_004e17a0 @ 0x004e17a0 performs spatial query using width and height
        ///   - FUN_004f5290 @ 0x004f5290 performs detailed collision detection
        /// - FUN_0041d2c0 @ 0x0041d2c0: 2DA table lookup function (equivalent to FUN_00413350 in K1)
        /// - FUN_0065a380 @ 0x0065a380: GetCreatureRadius wrapper, calls FUN_0041d2c0
        /// - Default width: 0.6f (not 0.5f) if appearance data unavailable
        /// - Default radius: 0.5f (medium creature size) if appearance data unavailable
        /// - Note: Original code uses width at +0x14 and height at +0xbc for collision detection, but we approximate with radius for all dimensions
        /// - Cross-engine difference: K2 uses offset 0x380 (K1 uses 0x340), width at +0x14 (K1 uses +4), height at +0xbc (K1 uses +8)
        /// </remarks>
        protected override CreatureBoundingBox GetCreatureBoundingBox(IEntity entity)
        {
            if (entity == null)
            {
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Default bounding box for null entity
                // Original code: Width defaults to 0.6f (0x3f19999a), radius defaults to 0.5f
                // For collision detection, we use radius for all dimensions
                return CreatureBoundingBox.FromRadius(0.5f); // Default radius
            }

            // Get appearance type from entity
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_0050e170 @ 0x0050e170 gets appearance type from offset 0x1184
            // Original code: `*(ushort *)(param_1 + 0x1184)` - direct memory access
            // Our abstraction: Use GetAppearanceType() which tries IRenderableComponent, then reflection
            // This matches the behavior but uses high-level API instead of direct memory access
            int appearanceType = GetAppearanceType(entity);

            // Get bounding box dimensions from GameDataProvider
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_0050e170 @ 0x0050e170 performs 5 separate 2DA lookups (similar to K1):
            // 1. First lookup (DAT_00826990): Sets width at +4 (defaults to 0.6f if lookup fails)
            // 2. Second lookup (DAT_00826948): Sets radius at +8 (this is the hitradius column)
            // 3-5. Additional lookups for other bounding box values
            // FUN_005479f0 @ 0x005479f0 uses width at +0x14 and height at +0xbc for collision detection
            // For our implementation, we use the radius (from second lookup) for all dimensions
            float radius = 0.5f; // Default radius (medium creature size)

            if (appearanceType >= 0 && entity.World != null && entity.World.GameDataProvider != null)
            {
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_0041d2c0 @ 0x0041d2c0 accesses "hitradius" column from appearance.2da
                // This is the second lookup in FUN_0050e170 (DAT_00826948), which sets radius at offset +8
                // FUN_0065a380 @ 0x0065a380 wraps FUN_0041d2c0 for GetCreatureRadius
                // OdysseyGameDataProvider.GetCreatureRadius uses TwoDATableManager to lookup hitradius
                // This matches the pattern in K1: FUN_0060e170 @ 0x0060e170 calls FUN_00413350 @ 0x00413350 for 2DA lookup
                radius = entity.World.GameDataProvider.GetCreatureRadius(appearanceType, 0.5f);
            }

            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) reverse engineering: Bounding box structure at offset 0x380
            // FUN_0050e170 @ 0x0050e170 (initialization) sets:
            // - Width at +4: `*(float *)(iVar1 + 4) = local_4;` (defaults to 0.6f if lookup fails)
            // - Radius at +8: `*(float *)(*(int *)(param_1 + 0x380) + 8) = local_4;` (from hitradius lookup)
            // FUN_005479f0 @ 0x005479f0 (collision detection) uses:
            // - Width at +0x14: `fVar8 = *(float *)(iVar1 + 0x14) + fVar9 + _DAT_007b6888;`
            // - Height at +0xbc: `uVar12 = *(undefined4 *)(iVar1 + 0xbc);`
            // For collision detection, we use radius for all dimensions (width, height, depth)
            // Original engine: K2 uses offset 0x380 (different from K1's 0x340), width at +0x14 (K1 uses +4), height at +0xbc (K1 uses +8)
            return new CreatureBoundingBox(radius, radius, radius);
        }
    }
}


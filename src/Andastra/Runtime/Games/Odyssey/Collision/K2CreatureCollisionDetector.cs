using Andastra.Runtime.Core.Collision;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Games.Odyssey.Collision
{
    /// <summary>
    /// KOTOR 2: The Sith Lords (swkotor2.exe) specific creature collision detection.
    /// </summary>
    /// <remarks>
    /// K2 Creature Collision Detection:
    /// - Based on swkotor2.exe reverse engineering via Ghidra MCP
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
        /// Based on swkotor2.exe: FUN_005479f0 @ 0x005479f0 uses creature bounding box from entity structure at offset 0x380.
        /// </summary>
        /// <param name="entity">The creature entity.</param>
        /// <returns>The creature's bounding box.</returns>
        /// <remarks>
        /// Based on swkotor2.exe reverse engineering via Ghidra MCP:
        /// - FUN_005479f0 @ 0x005479f0 accesses bounding box via: `*(int *)((int)this + 0x380)`
        /// - Width stored at offset +0x14: `fVar8 = *(float *)(iVar1 + 0x14) + fVar9 + _DAT_007b6888;`
        /// - Height stored at offset +0xbc: `uVar12 = *(undefined4 *)(iVar1 + 0xbc);`
        /// - FUN_004e17a0 @ 0x004e17a0 performs spatial query using bounding box dimensions
        /// - FUN_004f5290 @ 0x004f5290 performs detailed collision detection
        /// - Gets appearance type from entity structure (offset varies, accessed via virtual function call)
        /// - Looks up hitradius from appearance.2da using FUN_0041d2c0 @ 0x0041d2c0 (2DA table lookup)
        /// - FUN_0065a380 @ 0x0065a380 wraps FUN_0041d2c0 for GetCreatureRadius functionality
        /// - Default radius: 0.5f (medium creature size) if appearance data unavailable
        /// - Fallback: Uses size category from appearance.2da if hitradius not available (same as K1)
        /// - Cross-engine difference: K2 uses offset 0x380 (K1 uses 0x340), width at +0x14 (K1 uses +4), height at +0xbc (K1 uses +8)
        /// </remarks>
        protected override CreatureBoundingBox GetCreatureBoundingBox(IEntity entity)
        {
            if (entity == null)
            {
                // Based on swkotor2.exe: Default bounding box for null entity (medium creature size)
                return CreatureBoundingBox.FromRadius(0.5f); // Default radius
            }

            // Get appearance type from entity
            // Based on swkotor2.exe: FUN_005479f0 @ 0x005479f0 gets appearance type from entity structure
            // Appearance type accessed via virtual function call or entity property
            int appearanceType = GetAppearanceType(entity);

            // Get bounding box dimensions from GameDataProvider
            // Based on swkotor2.exe: FUN_005479f0 gets width and height from entity structure
            // Width stored at offset 0x380 + 0x14, height at offset 0x380 + 0xbc
            // For now, we use hitradius from appearance.2da as the base radius
            // The original engine uses width and height separately, but we approximate with radius
            float radius = 0.5f; // Default radius

            if (appearanceType >= 0 && entity.World != null && entity.World.GameDataProvider != null)
            {
                // Based on swkotor2.exe: FUN_0041d2c0 @ 0x0041d2c0 accesses "hitradius" column from appearance.2da
                // FUN_0065a380 @ 0x0065a380 wraps FUN_0041d2c0 for GetCreatureRadius
                // OdysseyGameDataProvider.GetCreatureRadius uses TwoDATableManager to lookup hitradius
                // This matches the pattern in K1: FUN_0060e170 @ 0x0060e170 calls FUN_00413350 @ 0x00413350 for 2DA lookup
                radius = entity.World.GameDataProvider.GetCreatureRadius(appearanceType, 0.5f);
            }

            // Based on swkotor2.exe reverse engineering: Bounding box structure at offset 0x380, width at +0x14, height at +0xbc
            // FUN_005479f0 @ 0x005479f0 uses: `fVar8 = *(float *)(iVar1 + 0x14) + fVar9 + _DAT_007b6888;` for width
            // FUN_005479f0 uses: `uVar12 = *(undefined4 *)(iVar1 + 0xbc);` for height
            // Bounding box uses same radius for width, height, and depth (spherical approximation)
            // Original engine: K2 uses offset 0x380 (different from K1's 0x340), width at +0x14 (K1 uses +4), height at +0xbc (K1 uses +8)
            return new CreatureBoundingBox(radius, radius, radius);
        }
    }
}


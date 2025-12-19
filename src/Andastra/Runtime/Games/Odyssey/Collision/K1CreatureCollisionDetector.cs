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
        /// Based on swkotor.exe reverse engineering via Ghidra MCP:
        /// - FUN_004ed6e0 @ 0x004ed6e0 accesses bounding box via: `*(int *)(param_1 + 0x340)`
        /// - Radius stored at offset +8 from bounding box pointer: `*(float *)(*(int *)(param_1 + 0x340) + 8)`
        /// - Width stored at offset +4: `*(float *)(iVar1 + 4)`
        /// - FUN_004f1310 @ 0x004f1310 adds two creature radii: `*(float *)(*(int *)(iVar3 + 0x340) + 8) + *(float *)(*(int *)(param_1 + 0x340) + 8)`
        /// - Gets appearance type from entity structure at offset 0xa60: `*(ushort *)(param_1 + 0xa60)`
        /// - Looks up hitradius from appearance.2da using FUN_00413350 @ 0x00413350 (2DA table lookup)
        /// - FUN_0060e170 @ 0x0060e170 wraps FUN_00413350 for GetCreatureRadius functionality
        /// - Default radius: 0.5f (medium creature size) if appearance data unavailable
        /// - Fallback: Uses size category from appearance.2da if hitradius not available (same as K2)
        /// </remarks>
        protected override CreatureBoundingBox GetCreatureBoundingBox(IEntity entity)
        {
            if (entity == null)
            {
                // Based on swkotor.exe: Default bounding box for null entity (medium creature size)
                return CreatureBoundingBox.FromRadius(0.5f); // Default radius
            }

            // Get appearance type from entity
            // Based on swkotor.exe: FUN_004ed6e0 @ 0x004ed6e0 gets appearance type from offset 0xa60
            // FUN_00413350 accesses: `(uint)*(ushort *)(param_1 + 0xa60)`
            int appearanceType = GetAppearanceType(entity);

            // Get bounding box dimensions from GameDataProvider
            // Based on swkotor.exe: FUN_004ed6e0 @ 0x004ed6e0 uses FUN_00413350 to lookup "hitradius" from appearance.2da
            // Located via string reference: "HITRADIUS" @ 0x0074b640 (swkotor.exe: appearance.2da column name)
            // FUN_0060e170 @ 0x0060e170 wraps FUN_00413350 for GetCreatureRadius (string reference @ 0x00742f1c)
            float radius = 0.5f; // Default radius (medium creature size)

            if (appearanceType >= 0 && entity.World != null && entity.World.GameDataProvider != null)
            {
                // Based on swkotor.exe: FUN_00413350 @ 0x00413350 accesses "hitradius" column from appearance.2da
                // OdysseyGameDataProvider.GetCreatureRadius uses TwoDATableManager to lookup hitradius
                // This matches the pattern in K2: FUN_0065a380 @ 0x0065a380 calls FUN_0041d2c0 @ 0x0041d2c0 for 2DA lookup
                radius = entity.World.GameDataProvider.GetCreatureRadius(appearanceType, 0.5f);
            }

            // Based on swkotor.exe reverse engineering: Bounding box structure at offset 0x340, radius at offset +8
            // FUN_004ed6e0 @ 0x004ed6e0 sets radius at: `*(float *)(*(int *)(param_1 + 0x340) + 8) = local_4;`
            // FUN_004f1310 @ 0x004f1310 adds two radii for collision distance: radius1 + radius2
            // Bounding box uses same radius for width, height, and depth (spherical approximation)
            // Original engine: K1 uses offset 0x340 (different from K2's 0x380), radius at +8, width at +4
            return new CreatureBoundingBox(radius, radius, radius);
        }
    }
}


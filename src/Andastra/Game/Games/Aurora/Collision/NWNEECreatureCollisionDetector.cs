using Andastra.Runtime.Core.Collision;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;

namespace Andastra.Game.Games.Aurora.Collision
{
    /// <summary>
    /// Neverwinter Nights: Enhanced Edition (nwmain.exe) specific creature collision detection.
    /// Overrides AuroraCreatureCollisionDetector to use NWN:EE-specific bounding box structure (offset 0x530).
    /// </summary>
    /// <remarks>
    /// NWN:EE Creature Collision Detection:
    /// - Based on nwmain.exe reverse engineering via Ghidra MCP
    /// - Bounding box structure pointer at offset 0x530 (same as original NWN)
    /// - Reverse engineered functions:
    ///   - CNWSCreature::AIActionCheckMoveToObjectRadius @ 0x1403b4580 (nwmain.exe: checks if creature can move to object within radius)
    ///   - CNWSCreature::AIActionCheckMoveToPointRadius @ 0x1403b5b00 (nwmain.exe: checks if creature can move to point within radius)
    ///   - CNWSCreature::GetUseRange @ 0x140396480 (nwmain.exe: gets use range for objects, accesses bounding box at offset 0x530)
    ///   - CNWSCreature::GetIsInUseRange @ 0x140391310 (nwmain.exe: checks if creature is in use range)
    ///   - CNWSObject::RunActions @ 0x1404ac010 (nwmain.exe: runs AI actions including radius checks)
    /// - Bounding box structure layout (offset 0x530):
    ///   - Radius at offset +8: `*(float *)(*(longlong *)(this + 0x530) + 8)`
    ///   - Width at offset +4: `*(float *)(*(longlong *)(this + 0x530) + 4)`
    /// - Located via string references:
    ///   - "AIActionCheckMoveToObjectRadius" @ 0x140de0b20 (nwmain.exe: function name string)
    ///   - "AIActionCheckMoveToPointRadius" @ 0x140de0c30 (nwmain.exe: function name string)
    ///   - "Only creatures can run ActionCheckMoveToObjectRadius" @ 0x140df1fd8 (nwmain.exe: error message)
    ///   - "Only creatures can run ActionCheckMoveToPointRadius" @ 0x140df22e0 (nwmain.exe: error message)
    /// - Cross-engine comparison:
    ///   - NWN:EE (nwmain.exe): Bounding box at offset 0x530, radius at +8, width at +4
    ///   - Original NWN (nwn.exe): Similar structure (needs verification)
    ///   - Common: Both use appearance.2da hitradius via C2DA::GetFloatingPoint
    /// - Inheritance structure:
    ///   - BaseCreatureCollisionDetector (Runtime.Core.Collision): Common collision detection logic
    ///   - AuroraCreatureCollisionDetector (Runtime.Games.Aurora.Collision): Common Aurora logic (defaults to NWN:EE)
    ///   - NWNEECreatureCollisionDetector (Runtime.Games.Aurora.Collision): NWN:EE-specific (nwmain.exe: offset 0x530)
    /// </remarks>
    public class NWNEECreatureCollisionDetector : AuroraCreatureCollisionDetector
    {
        /// <summary>
        /// Gets the bounding box for a creature entity.
        /// Based on nwmain.exe: CNWSCreature::GetUseRange @ 0x140396480 uses bounding box structure at offset 0x530.
        /// </summary>
        /// <param name="entity">The creature entity.</param>
        /// <returns>The creature's bounding box.</returns>
        /// <remarks>
        /// Based on nwmain.exe reverse engineering via Ghidra MCP:
        /// - CNWSCreature::GetUseRange @ 0x140396480:
        ///   - Line 27: Default radius (width): `*param_3 = *(float *)(*(longlong *)(this + 0x530) + 4);` - width at +4
        ///   - Line 35: For creatures: `*param_3 = *(float *)(*(longlong *)(this + 0x530) + 8);` - radius at +8
        ///   - Line 41: Adds two radii: `*param_3 = *(float *)(*(longlong *)(lVar5 + 0x530) + 8) + *param_3;`
        /// - CNWSArea::NoCreaturesOnLine @ 0x14036ec90 (collision detection):
        ///   - Line 113: Uses radius: `fVar14 = *(float *)(*(longlong *)(pCVar7 + 0x530) + 8);`
        ///   - Line 134: Adds two radii: `fVar18 = local_fc + *(float *)(*(longlong *)(pCVar7 + 0x530) + 8);`
        /// - CNWSCreature::BumpFriends @ 0x140385130 (creature-to-creature collision):
        ///   - Line 52-53: Adds two radii: `*(float *)(*(longlong *)(this + 0x530) + 8) + DAT_140d83b74 + *(float *)(*(longlong *)(param_1 + 0x530) + 8);`
        /// - CNWSCreature::AIActionCheckMoveToObjectRadius @ 0x1403b4580 calls GetUseRange to get creature radius
        /// - CNWSCreature::AIActionCheckMoveToPointRadius @ 0x1403b5b00 uses radius parameter from action node
        /// - Note: GetUseRange doesn't show appearance type lookup - it directly uses bounding box structure
        /// - Looks up hitradius from appearance.2da using C2DA::GetFloatingPoint (done elsewhere, not in GetUseRange)
        /// - Default radius: 0.5f (medium creature size) if appearance data unavailable
        /// </remarks>
        protected override CreatureBoundingBox GetCreatureBoundingBox(IEntity entity)
        {
            if (entity == null)
            {
                // Based on nwmain.exe: Default bounding box for null entity (medium creature size)
                // CNWSCreature::GetUseRange @ 0x140396480 line 27: Default uses width at +4
                // For creatures (line 35), uses radius at +8
                return CreatureBoundingBox.FromRadius(0.5f); // Default radius
            }

            // Get appearance type from entity
            // Based on nwmain.exe: CNWSCreature::GetUseRange doesn't show appearance type lookup
            // It directly accesses bounding box structure at offset 0x530
            // Appearance type lookup is done elsewhere (likely during creature initialization)
            // Our abstraction: Use GetAppearanceType() which tries IRenderableComponent, then reflection
            // This matches the behavior but uses high-level API instead of direct memory access
            int appearanceType = -1;

            // First, try to get appearance type from IRenderableComponent
            IRenderableComponent renderable = entity.GetComponent<IRenderableComponent>();
            if (renderable != null)
            {
                appearanceType = renderable.AppearanceRow;
            }

            // If not found, try to get appearance type from engine-specific creature component using reflection
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
            // Based on nwmain.exe: CNWSCreature::GetUseRange uses bounding box structure directly
            // The radius at offset +8 is set from appearance.2da hitradius (lookup done elsewhere)
            // CNWSArea::NoCreaturesOnLine @ 0x14036ec90 uses radius at +8 for collision detection
            // Located via string reference: "hitradius" column in appearance.2da lookup
            float radius = 0.5f; // Default radius (medium creature size)

            if (appearanceType >= 0 && entity.World != null && entity.World.GameDataProvider != null)
            {
                // Based on nwmain.exe: C2DA::GetFloatingPoint accesses "hitradius" column from appearance.2da
                // This is done during creature initialization, not in GetUseRange
                // AuroraGameDataProvider.GetCreatureRadius uses AuroraTwoDATableManager to lookup hitradius
                radius = entity.World.GameDataProvider.GetCreatureRadius(appearanceType, 0.5f);
            }

            // Based on nwmain.exe reverse engineering: Bounding box structure at offset 0x530
            // CNWSCreature::GetUseRange @ 0x140396480:
            // - Line 27: Default uses width at +4: `*param_3 = *(float *)(*(longlong *)(this + 0x530) + 4);`
            // - Line 35: For creatures uses radius at +8: `*param_3 = *(float *)(*(longlong *)(this + 0x530) + 8);`
            // CNWSArea::NoCreaturesOnLine @ 0x14036ec90 uses radius at +8: `fVar14 = *(float *)(*(longlong *)(pCVar7 + 0x530) + 8);`
            // CNWSCreature::BumpFriends @ 0x140385130 adds two radii: radius1 + radius2 + DAT_140d83b74
            // Bounding box uses same radius for width, height, and depth (spherical approximation)
            // Original engine: NWN:EE uses offset 0x530 (same as original NWN), radius at +8, width at +4
            return new CreatureBoundingBox(radius, radius, radius);
        }
    }
}


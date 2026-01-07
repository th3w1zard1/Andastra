using System.Numerics;

namespace Andastra.Runtime.Scripting.Types
{
    /// <summary>
    /// Represents a location (position + facing) in NWScript.
    /// </summary>
    /// <remarks>
    /// Location Type:
    /// - Based on swkotor2.exe location type system
    /// - Located via string references: "LOCATION" @ 0x007c2850 (location type constant), "ValLocation" @ 0x007c26ac (location value field)
    /// - "CatLocation" @ 0x007c26dc (location catalog field), "FollowLocation" @ 0x007beda8 (follow location field)
    /// - Error messages:
    ///   - "Script var '%s' not a LOCATION!" @ 0x007c25e0 (location type check error)
    ///   - "Script var LOCATION '%s' not in catalogue!" @ 0x007c2600 (location catalog error)
    ///   - "ReadTableWithCat(): LOCATION '%s' won't fit!" @ 0x007c2734 (location table read error)
    /// - Original implementation: Location is a complex type in NWScript containing position (Vector3) and facing (float)
    /// - Location storage: Stored in script variable system with catalog references for persistence
    /// - Location usage: Used by NWScript functions like GetLocation, CreateLocation, GetPosition, GetFacing
    /// - Position: Vector3 (X, Y, Z) in world coordinates
    /// - Facing: Float angle in radians (0 = +X axis, counter-clockwise positive for 2D gameplay)
    /// - Based on NWScript location type semantics from vendor/PyKotor/wiki/
    /// </remarks>
    public class Location
    {
        public Vector3 Position { get; set; }
        public float Facing { get; set; }

        public Location()
        {
            Position = Vector3.Zero;
            Facing = 0f;
        }

        public Location(Vector3 position, float facing)
        {
            Position = position;
            Facing = facing;
        }
    }
}


namespace BioWare.NET.Resource.Formats.GFF.Generics.ARE
{
    /// <summary>
    /// Enumeration for ARE map north axis orientation.
    /// </summary>
    /// <remarks>
    /// WHAT IS NORTH AXIS?
    /// 
    /// NorthAxis tells the game engine which direction is "north" on the minimap. In the
    /// real world, north is always the same direction. But in a game, the area might be
    /// oriented differently, so we need to tell the engine which axis points north.
    /// 
    /// WHY IS IT NEEDED?
    /// 
    /// The minimap needs to know which direction is north so it can:
    /// - Display the compass correctly
    /// - Orient the map image properly
    /// - Show waypoints and markers in the correct positions
    /// 
    /// Without this, the minimap might be rotated incorrectly, making it confusing for players.
    /// 
    /// THE VALUES:
    /// 
    /// - PositiveY (0): The positive Y axis points north
    ///   - This means moving in the +Y direction is moving north
    ///   - This is the most common orientation
    /// 
    /// - NegativeY (1): The negative Y axis points north
    ///   - This means moving in the -Y direction is moving north
    ///   - Used when the area is rotated 180 degrees
    /// 
    /// - PositiveX (2): The positive X axis points north
    ///   - This means moving in the +X direction is moving north
    ///   - Used when the area is rotated 90 degrees clockwise
    /// 
    /// - NegativeX (3): The negative X axis points north
    ///   - This means moving in the -X direction is moving north
    ///   - Used when the area is rotated 90 degrees counter-clockwise
    /// 
    /// HOW IT WORKS:
    /// 
    /// When the game engine displays the minimap:
    /// 1. It reads the NorthAxis value from the ARE file
    /// 2. It determines which axis points north based on this value
    /// 3. It rotates the map image so that north is at the top
    /// 4. It orients all map elements (waypoints, markers) accordingly
    /// 
    /// EXAMPLE:
    /// 
    /// If NorthAxis = PositiveY:
    /// - Moving in the +Y direction moves north on the map
    /// - The map is displayed with +Y at the top
    /// - The compass points in the +Y direction
    /// 
    /// If NorthAxis = PositiveX:
    /// - Moving in the +X direction moves north on the map
    /// - The map is displayed with +X at the top
    /// - The map is rotated 90 degrees clockwise from the default
    /// 
    /// ORIGINAL IMPLEMENTATION:
    /// 
    /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): The NorthAxis value is read from the ARE file's Map structure
    /// and used to orient the minimap display. The default value is PositiveX (2), but most
    /// areas use PositiveY (0).
    /// 
    /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:387-391
    /// Original: class ARENorthAxis(IntEnum):
    /// </remarks>
    public enum ARENorthAxis
    {
        /// <summary>
        /// Positive Y axis points north (0).
        /// Moving in the +Y direction is moving north. This is the most common orientation.
        /// </summary>
        PositiveY = 0,

        /// <summary>
        /// Negative Y axis points north (1).
        /// Moving in the -Y direction is moving north. Used when the area is rotated 180 degrees.
        /// </summary>
        NegativeY = 1,

        /// <summary>
        /// Positive X axis points north (2).
        /// Moving in the +X direction is moving north. Used when the area is rotated 90 degrees clockwise.
        /// </summary>
        PositiveX = 2,

        /// <summary>
        /// Negative X axis points north (3).
        /// Moving in the -X direction is moving north. Used when the area is rotated 90 degrees counter-clockwise.
        /// </summary>
        NegativeX = 3
    }
}


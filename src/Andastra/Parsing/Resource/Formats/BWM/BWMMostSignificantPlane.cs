namespace Andastra.Parsing.Formats.BWM
{
    /// <summary>
    /// Represents the most significant plane (axis) used for AABB tree node splitting.
    /// This is used when building the AABB tree to determine which axis to split along.
    /// </summary>
    /// <remarks>
    /// WHAT IS A MOST SIGNIFICANT PLANE?
    /// 
    /// When building an AABB tree, we need to split triangles into two groups (left and right).
    /// To do this, we choose an axis (X, Y, or Z) and split along that axis. The "most significant
    /// plane" tells us which axis was chosen for splitting.
    /// 
    /// HOW IS IT USED?
    /// 
    /// When building the AABB tree:
    /// 1. Calculate the bounding box for all triangles in the current node
    /// 2. Find the longest dimension (X, Y, or Z) of the bounding box
    /// 3. Choose that dimension as the split axis
    /// 4. Store the chosen axis in the BWMMostSignificantPlane enum
    /// 5. Split triangles based on their center position along that axis
    /// 
    /// WHY SPLIT ALONG THE LONGEST AXIS?
    /// 
    /// Splitting along the longest axis creates a more balanced tree. If we split along a short
    /// axis, we might end up with most triangles on one side and very few on the other, creating
    /// an unbalanced tree. An unbalanced tree is slower to search.
    /// 
    /// THE VALUES:
    /// 
    /// Negative values (-3, -2, -1) represent negative directions along each axis:
    /// - NegativeZ = -3: Split along negative Z axis
    /// - NegativeY = -2: Split along negative Y axis
    /// - NegativeX = -1: Split along negative X axis
    /// 
    /// Positive values (1, 2, 3) represent positive directions along each axis:
    /// - PositiveX = 1: Split along positive X axis
    /// - PositiveY = 2: Split along positive Y axis
    /// - PositiveZ = 3: Split along positive Z axis
    /// 
    /// None = 0: No split axis (used for leaf nodes that contain only one triangle)
    /// 
    /// HOW IT'S STORED:
    /// 
    /// The most significant plane is stored in each AABB tree node (BWMNodeAABB). When traversing
    /// the tree, this value can be used to optimize searches, though in practice, most implementations
    /// recalculate the split axis during traversal rather than using the stored value.
    /// 
    /// ORIGINAL IMPLEMENTATION:
    /// 
    /// Based on swkotor2.exe: The most significant plane is stored in the AABB tree nodes when
    /// the tree is built. The original engine uses this value to optimize tree traversal, though
    /// the exact optimization details are not fully documented.
    /// 
    /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/bwm_data.py:1534-1541
    /// Original: class BWMMostSignificantPlane(IntEnum)
    /// </remarks>
    public enum BWMMostSignificantPlane
    {
        /// <summary>
        /// Split along negative Z axis (downward direction).
        /// </summary>
        NegativeZ = -3,

        /// <summary>
        /// Split along negative Y axis (backward direction).
        /// </summary>
        NegativeY = -2,

        /// <summary>
        /// Split along negative X axis (leftward direction).
        /// </summary>
        NegativeX = -1,

        /// <summary>
        /// No split axis (used for leaf nodes containing only one triangle).
        /// </summary>
        None = 0,

        /// <summary>
        /// Split along positive X axis (rightward direction).
        /// </summary>
        PositiveX = 1,

        /// <summary>
        /// Split along positive Y axis (forward direction).
        /// </summary>
        PositiveY = 2,

        /// <summary>
        /// Split along positive Z axis (upward direction).
        /// </summary>
        PositiveZ = 3
    }
}


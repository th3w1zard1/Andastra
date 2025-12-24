using System;
using System.Numerics;
using Andastra.Parsing;
using Andastra.Parsing.Common;

namespace Andastra.Parsing.Formats.BWM
{
    /// <summary>
    /// Represents a node in the AABB (Axis-Aligned Bounding Box) tree stored in a BWM file.
    /// This is the pre-built spatial acceleration structure that comes with WOK area walkmeshes.
    /// </summary>
    /// <remarks>
    /// WHAT IS A BWMNODEAABB?
    /// 
    /// A BWMNodeAABB is a single node in a tree structure that helps the game quickly find which
    /// triangles are near a given point. It's like a filing cabinet where files (triangles) are
    /// organized into drawers (boxes) that are organized into bigger drawers (bigger boxes).
    /// 
    /// The AABB tree is stored directly in the BWM file for WOK (area) walkmeshes. When the game
    /// loads a walkmesh, it can use this pre-built tree instead of building a new one from scratch.
    /// This saves time during loading.
    /// 
    /// WHAT DATA DOES IT STORE?
    /// 
    /// A BWMNodeAABB stores:
    /// 1. BbMin: The minimum corner of the bounding box (smallest x, y, z values)
    ///    - This is the "bottom-left-front" corner of the box
    ///    - Example: If the box contains points from (0, 0, 0) to (10, 5, 3), BbMin = (0, 0, 0)
    /// 
    /// 2. BbMax: The maximum corner of the bounding box (largest x, y, z values)
    ///    - This is the "top-right-back" corner of the box
    ///    - Example: If the box contains points from (0, 0, 0) to (10, 5, 3), BbMax = (10, 5, 3)
    /// 
    /// 3. Face: The triangle (BWMFace) stored at this node (if this is a leaf node)
    ///    - Leaf nodes contain actual triangles
    ///    - Internal nodes have Face = null (they only contain child nodes)
    /// 
    /// 4. Sigplane: The "most significant plane" used to split this node
    ///    - This tells us which axis (X, Y, or Z) was used to divide triangles into left and right
    ///    - Used when building the tree to decide how to organize triangles
    ///    - Values: 0 = X-axis, 1 = Y-axis, 2 = Z-axis
    /// 
    /// 5. Left: The left child node (triangles on one side of the split)
    ///    - If this is a leaf node, Left = null
    ///    - If this is an internal node, Left points to another BWMNodeAABB
    /// 
    /// 6. Right: The right child node (triangles on the other side of the split)
    ///    - If this is a leaf node, Right = null
    ///    - If this is an internal node, Right points to another BWMNodeAABB
    /// 
    /// HOW DOES THE TREE WORK?
    /// 
    /// The AABB tree is a binary tree (each node has at most 2 children). Here's how it works:
    /// 
    /// STEP 1: Root Node
    /// - The root node contains a bounding box that surrounds ALL triangles in the walkmesh
    /// - BbMin = smallest x, y, z of all vertices
    /// - BbMax = largest x, y, z of all vertices
    /// 
    /// STEP 2: Splitting
    /// - The root node is split into two child nodes (Left and Right)
    /// - The split happens along one axis (X, Y, or Z) - this is the "most significant plane"
    /// - Triangles are divided: some go to Left, some go to Right
    /// - Each child gets its own bounding box (smaller than the parent)
    /// 
    /// STEP 3: Recursive Splitting
    /// - Each child node is split again (if it has enough triangles)
    /// - This continues until each leaf node contains just one triangle
    /// - The result is a tree where:
    ///   - Internal nodes: Have Left and Right children, Face = null
    ///   - Leaf nodes: Have Left = null, Right = null, Face = actual triangle
    /// 
    /// STEP 4: Searching
    /// - To find which triangle contains a point, start at the root
    /// - Check if the point is inside the root's bounding box
    /// - If yes, check the left child, then the right child
    /// - Continue down the tree until you reach a leaf node
    /// - Check if the point is inside the leaf's triangle
    /// 
    /// WHY IS IT FAST?
    /// 
    /// Without the AABB tree, to find which triangle contains a point, you'd need to check EVERY
    /// triangle in the walkmesh. If there are 10,000 triangles, that's 10,000 checks.
    /// 
    /// With the AABB tree, you only check a few nodes (maybe 10-20) because:
    /// - You can quickly skip entire branches if the point is outside their bounding boxes
    /// - You only check triangles that are actually near the point
    /// - The tree is balanced (roughly equal triangles on left and right)
    /// 
    /// Example: If you have 10,000 triangles, the tree has about 14 levels (2^14 = 16,384).
    /// To find a triangle, you check about 14 nodes instead of 10,000 triangles. That's 700x faster!
    /// 
    /// LEAF NODES VS INTERNAL NODES:
    /// 
    /// Leaf Node (contains a triangle):
    /// - Face != null (points to a BWMFace)
    /// - Left = null (no children)
    /// - Right = null (no children)
    /// - BbMin and BbMax form a box around the triangle
    /// 
    /// Internal Node (contains child nodes):
    /// - Face = null (no triangle here)
    /// - Left != null (has a left child)
    /// - Right != null (has a right child)
    /// - BbMin and BbMax form a box that contains both children's boxes
    /// 
    /// THE MOST SIGNIFICANT PLANE:
    /// 
    /// When building the tree, we need to decide how to split triangles into left and right.
    /// We choose the axis (X, Y, or Z) where triangles are spread out the most. This is the
    /// "most significant plane" because it's the axis that matters most for organizing triangles.
    /// 
    /// For example:
    /// - If triangles are spread out mostly along the X-axis (east-west), split on X
    /// - If triangles are spread out mostly along the Y-axis (north-south), split on Y
    /// - If triangles are spread out mostly along the Z-axis (up-down), split on Z
    /// 
    /// This ensures the tree is balanced and efficient.
    /// 
    /// WHEN IS IT USED?
    /// 
    /// The BWMNodeAABB tree is used when:
    /// 1. Loading a WOK file: The tree is read from the file and used directly
    /// 2. Converting to NavigationMesh: The tree is converted to NavigationMesh.AabbNode format
    /// 3. Building a new tree: If the tree is missing or invalid, a new one is built from triangles
    /// 
    /// CRITICAL: ONLY AREA WALKMESHES HAVE THIS:
    /// 
    /// Only WOK (AreaModel) walkmeshes have AABB trees stored in the file. PWK/DWK (PlaceableOrDoor)
    /// walkmeshes do NOT have AABB trees because:
    /// - They're small (maybe 10-100 triangles)
    /// - Brute force checking is fast enough for small walkmeshes
    /// - They don't need pathfinding or complex spatial queries
    /// 
    /// If you try to use BWM.Aabbs() on a PlaceableOrDoor walkmesh, it will return an empty list
    /// or build a temporary tree, but it won't be saved to the file.
    /// 
    /// ORIGINAL IMPLEMENTATION:
    /// 
    /// Based on swkotor2.exe: The AABB tree is stored in the BWM file format as a binary tree
    /// structure. Each node contains bounding box coordinates, a triangle reference (for leaf nodes),
    /// a split plane indicator, and child node references.
    /// 
    /// The original engine uses this pre-built tree for fast spatial queries during gameplay.
    /// When loading a walkmesh, the engine reads the tree from the file and uses it directly,
    /// avoiding the need to rebuild it from scratch.
    /// 
    /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/bwm_data.py:1544-1707
    /// Original: class BWMNodeAABB(ComparableMixin)
    /// </remarks>
    public class BWMNodeAABB
    {
        /// <summary>
        /// The minimum corner of the bounding box (smallest x, y, z values).
        /// This is the "bottom-left-front" corner of the box that contains this node's triangles.
        /// </summary>
        public Vector3 BbMin { get; set; }

        /// <summary>
        /// The maximum corner of the bounding box (largest x, y, z values).
        /// This is the "top-right-back" corner of the box that contains this node's triangles.
        /// </summary>
        public Vector3 BbMax { get; set; }

        /// <summary>
        /// The triangle (BWMFace) stored at this node, if this is a leaf node.
        /// If this is an internal node, Face = null (it only contains child nodes).
        /// </summary>
        public BWMFace Face { get; set; }

        /// <summary>
        /// The "most significant plane" used to split this node.
        /// Indicates which axis (X=0, Y=1, Z=2) was used to divide triangles into left and right.
        /// </summary>
        public BWMMostSignificantPlane Sigplane { get; set; }

        /// <summary>
        /// The left child node (triangles on one side of the split).
        /// If this is a leaf node, Left = null.
        /// </summary>
        public BWMNodeAABB Left { get; set; }

        /// <summary>
        /// The right child node (triangles on the other side of the split).
        /// If this is a leaf node, Right = null.
        /// </summary>
        public BWMNodeAABB Right { get; set; }

        /// <summary>
        /// Creates a new BWMNodeAABB with the specified properties.
        /// </summary>
        /// <param name="bbMin">Minimum corner of the bounding box</param>
        /// <param name="bbMax">Maximum corner of the bounding box</param>
        /// <param name="face">The triangle stored at this node (null for internal nodes)</param>
        /// <param name="sigplane">The most significant plane (0=X, 1=Y, 2=Z)</param>
        /// <param name="left">Left child node (null for leaf nodes)</param>
        /// <param name="right">Right child node (null for leaf nodes)</param>
        public BWMNodeAABB(Vector3 bbMin, Vector3 bbMax, BWMFace face, int sigplane, BWMNodeAABB left, BWMNodeAABB right)
        {
            BbMin = bbMin;
            BbMax = bbMax;
            Face = face;
            Sigplane = (BWMMostSignificantPlane)sigplane;
            Left = left;
            Right = right;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is BWMNodeAABB other))
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            return BbMin.Equals(other.BbMin) &&
                   BbMax.Equals(other.BbMax) &&
                   Face == other.Face &&
                   Sigplane == other.Sigplane &&
                   Left == other.Left &&
                   Right == other.Right;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(BbMin, BbMax, Face, Sigplane, Left, Right);
        }
    }
}

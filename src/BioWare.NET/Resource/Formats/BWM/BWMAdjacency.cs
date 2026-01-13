using System;
using BioWare.NET.Common;

namespace BioWare.NET.Resource.Formats.BWM
{
    /// <summary>
    /// Represents adjacency information between two walkmesh faces.
    /// Stores which face is adjacent and which edge of that face connects to the current face.
    /// </summary>
    /// <remarks>
    /// WHAT IS ADJACENCY?
    /// 
    /// Adjacency tells which triangles share edges with each other. Two triangles are adjacent
    /// (neighbors) if they share exactly two vertices, which forms a shared edge. This information
    /// is critical for pathfinding because the pathfinding algorithm needs to know which triangles
    /// can be reached from the current triangle.
    /// 
    /// WHAT DATA DOES IT STORE?
    /// 
    /// A BWMAdjacency stores:
    /// 1. Face: The adjacent (neighbor) triangle that shares an edge
    /// 2. Edge: Which edge of the neighbor triangle connects to the current face
    ///    - Edge 0: V1 -> V2
    ///    - Edge 1: V2 -> V3
    ///    - Edge 2: V3 -> V1
    /// 
    /// HOW IS IT USED?
    /// 
    /// When computing adjacency for a walkmesh, the algorithm:
    /// 1. Looks at each edge of each triangle
    /// 2. Finds if any other triangle shares that same edge
    /// 3. If found, creates a BWMAdjacency object linking them
    /// 4. Stores this in the adjacency array using encoding: faceIndex * 3 + edgeIndex
    /// 
    /// For example, if face 5's edge 1 is adjacent to face 12's edge 2:
    /// - The adjacency for face 5, edge 1 would be: BWMAdjacency(face12, edge2)
    /// - This is stored at index 5*3+1 = 16 in the adjacency array
    /// - The value stored is: 12*3+2 = 38 (encoding of face 12, edge 2)
    /// 
    /// BIDIRECTIONAL LINKING:
    /// 
    /// Adjacency is always bidirectional. If face A's edge connects to face B's edge, then:
    /// - Face A's adjacency points to face B
    /// - Face B's adjacency points to face A
    /// 
    /// This ensures pathfinding can traverse in both directions along shared edges.
    /// 
    /// ORIGINAL IMPLEMENTATION:
    /// 
    /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Adjacency is computed during walkmesh loading by finding edges
    /// that share the same two vertices. The adjacency data is then stored in the format
    /// faceIndex * 3 + edgeIndex for efficient lookup during pathfinding.
    /// </remarks>
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/bwm_data.py:1710-1762
    // Original: class BWMAdjacency(ComparableMixin)
    public class BWMAdjacency
    {
        /// <summary>
        /// The adjacent (neighbor) face that shares an edge with the current face.
        /// </summary>
        public BWMFace Face { get; set; }

        /// <summary>
        /// The edge index (0, 1, or 2) of the neighbor face that connects to the current face.
        /// </summary>
        public int Edge { get; set; }

        /// <summary>
        /// Creates a new BWMAdjacency linking a face to one of its edges.
        /// </summary>
        /// <param name="face">The adjacent face</param>
        /// <param name="index">The edge index (0, 1, or 2) of the adjacent face</param>
        public BWMAdjacency(BWMFace face, int index)
        {
            Face = face;
            Edge = index;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is BWMAdjacency other))
            {
                return false;
            }
            return Face == other.Face && Edge == other.Edge;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Face, Edge);
        }
    }
}

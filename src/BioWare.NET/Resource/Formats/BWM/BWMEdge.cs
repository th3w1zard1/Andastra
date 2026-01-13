using System;
using BioWare.NET.Common;

namespace BioWare.NET.Resource.Formats.BWM
{
    /// <summary>
    /// Represents a perimeter edge of a walkmesh with transition information.
    /// Used for area boundaries and door connections in the indoor map builder.
    /// </summary>
    /// <remarks>
    /// WHAT IS A BWMEDGE?
    ///
    /// A BWMEdge represents an edge of a walkmesh triangle that is on the perimeter (boundary)
    /// of the walkable area. Perimeter edges are edges that don't have a neighboring triangle
    /// on one side - they form the outer boundary of the walkmesh.
    ///
    /// WHAT DATA DOES IT STORE?
    ///
    /// A BWMEdge stores:
    /// 1. Face: The triangle that contains this edge
    /// 2. Index: The global edge index (faceIndex * 3 + localEdgeIndex)
    ///    - This uniquely identifies the edge across the entire walkmesh
    /// 3. Transition: The index of the connected room/area, or -1 if no connection
    ///    - Used for door placement and area boundaries
    ///    - When doors are placed, they use transitions to know where to connect
    /// 4. Final: Whether this is the final edge in a perimeter loop
    ///    - Perimeter edges form closed loops around walkable areas
    ///    - The Final flag marks the last edge in each loop
    ///
    /// HOW ARE EDGES IDENTIFIED AS PERIMETER?
    ///
    /// An edge is a perimeter edge if:
    /// 1. It belongs to a walkable face (non-walkable faces don't have perimeter edges)
    /// 2. It doesn't have an adjacent neighbor face (no other triangle shares that edge)
    ///
    /// The BWM.Edges() method finds all perimeter edges by:
    /// 1. Getting all walkable faces
    /// 2. Computing adjacency for each walkable face
    /// 3. Finding edges that have no adjacency (null adjacency = perimeter edge)
    /// 4. Following the perimeter loop by walking along connected perimeter edges
    ///
    /// TRANSITIONS AND DOOR PLACEMENT:
    ///
    /// Transitions tell the game which rooms or areas are connected at this edge. When you
    /// place a door in the indoor map builder, it uses transitions to know where doors should go.
    ///
    /// The transition value comes from the face's Trans1, Trans2, or Trans3 property, depending
    /// on which edge this is:
    /// - Edge 0 (V1->V2): Uses face.Trans1
    /// - Edge 1 (V2->V3): Uses face.Trans2
    /// - Edge 2 (V3->V1): Uses face.Trans3
    ///
    /// When the indoor map builder processes a room's walkmesh, it remaps transitions from dummy
    /// indices (from the kit component) to actual room indices (in the built module). This is done
    /// by calling BWM.RemapTransitions() for each hook connection, which updates all faces that have
    /// a transition matching the dummy index to use the actual room index instead. This allows rooms
    /// to be connected together properly when the module is built.
    ///
    /// PERIMETER LOOPS:
    ///
    /// Perimeter edges form closed loops around walkable areas. The algorithm:
    /// 1. Starts at a perimeter edge
    /// 2. Follows connected perimeter edges in a loop
    /// 3. Marks the last edge in each loop with Final = true
    /// 4. Continues until all perimeter edges are processed
    ///
    /// These loops define the boundaries of walkable areas and are used for:
    /// - Door placement (doors go on perimeter edges with transitions)
    /// - Area visualization (drawing boundaries)
    /// - Collision detection (knowing where walkable area ends)
    ///
    /// ORIGINAL IMPLEMENTATION:
    ///
    /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Perimeter edges are computed during walkmesh loading by finding
    /// edges of walkable faces that don't have adjacent neighbors. The edges are stored with
    /// transition information for door placement and area boundaries.
    /// </remarks>
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/bwm_data.py:1765-1843
    // Original: class BWMEdge(ComparableMixin)
    public class BWMEdge
    {
        /// <summary>
        /// The face (triangle) that contains this edge.
        /// </summary>
        public BWMFace Face { get; set; }

        /// <summary>
        /// The global edge index (faceIndex * 3 + localEdgeIndex).
        /// This uniquely identifies the edge across the entire walkmesh.
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// The transition index (room/area connection), or -1 if no connection.
        /// Used for door placement and area boundaries.
        /// </summary>
        public int Transition { get; set; }

        /// <summary>
        /// Whether this is the final edge in a perimeter loop.
        /// Perimeter edges form closed loops, and Final marks the last edge in each loop.
        /// </summary>
        public bool Final { get; set; }

        /// <summary>
        /// Creates a new BWMEdge representing a perimeter edge.
        /// </summary>
        /// <param name="face">The face containing this edge</param>
        /// <param name="index">The global edge index (faceIndex * 3 + localEdgeIndex)</param>
        /// <param name="transition">The transition index (room/area connection), or -1 if none</param>
        /// <param name="final">Whether this is the final edge in a perimeter loop</param>
        public BWMEdge(BWMFace face, int index, int transition, bool final = false)
        {
            Face = face;
            Index = index;
            Transition = transition;
            Final = final;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is BWMEdge other))
            {
                return false;
            }
            return Face == other.Face && Index == other.Index && Transition == other.Transition && Final == other.Final;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Face, Index, Transition, Final);
        }
    }
}

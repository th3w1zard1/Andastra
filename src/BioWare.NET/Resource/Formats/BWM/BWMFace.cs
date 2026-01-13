using System;
using System.Numerics;
using BioWare.NET.Common;

namespace BioWare.NET.Resource.Formats.BWM
{
    /// <summary>
    /// Represents a single triangle (face) in a BWM walkmesh.
    /// Extends the base Face class with transition information for area boundaries and door connections.
    /// </summary>
    /// <remarks>
    /// WHAT IS A BWMFACE?
    ///
    /// A BWMFace is a single triangle in a walkmesh. It represents one small piece of the walkable
    /// surface. The walkmesh is made up of many BWMFace objects, each one a triangle that covers
    /// part of the ground.
    ///
    /// WHAT DATA DOES IT STORE?
    ///
    /// A BWMFace stores:
    /// 1. Three vertices (V1, V2, V3): The three corner points of the triangle
    ///    - Each vertex has x, y, z coordinates
    ///    - These define the shape and position of the triangle
    ///
    /// 2. Material: The surface material type (0-30)
    ///    - Determines if the triangle is walkable
    ///    - Affects movement speed and pathfinding cost
    ///    - Examples: Stone (4), Grass (3), Water (6), Lava (15)
    ///    - Inherited from base Face class
    ///
    /// 3. Transitions (Trans1, Trans2, Trans3): Door/area connection information
    ///    - One transition per edge of the triangle
    ///    - Edge 0 (V1->V2) has Trans1
    ///    - Edge 1 (V2->V3) has Trans2
    ///    - Edge 2 (V3->V1) has Trans3
    ///    - Transition value is the index of the connected room/area, or null if no connection
    ///    - Used for door placement and area boundaries
    ///
    /// HOW ARE TRANSITIONS USED?
    ///
    /// Transitions tell the game which rooms or areas are connected at each edge of the triangle.
    /// When you place a door in the indoor map builder, it uses transitions to know where doors
    /// should go. The transition value is an index into the list of rooms in the module.
    ///
    /// For example:
    /// - If Trans1 = 5, it means edge 0 connects to room index 5
    /// - If Trans1 = null, it means edge 0 has no connection (is a boundary or wall)
    ///
    /// When the indoor map builder processes a room's walkmesh, it remaps transitions from dummy
    /// indices (from the kit component) to actual room indices (in the built module). This is done
    /// by calling BWM.RemapTransitions() for each hook connection. The method finds all faces where
    /// Trans1, Trans2, or Trans3 matches the dummy index and replaces it with the actual room index.
    /// This allows rooms to be connected together properly - when a door is placed between two rooms,
    /// the transition indices in both rooms' walkmeshes are updated to reference each other.
    ///
    /// EDGE NUMBERING:
    ///
    /// Each triangle has 3 edges, numbered 0, 1, and 2:
    /// - Edge 0: From vertex V1 to vertex V2 (V1 -> V2)
    /// - Edge 1: From vertex V2 to vertex V3 (V2 -> V3)
    /// - Edge 2: From vertex V3 to vertex V1 (V3 -> V1)
    ///
    /// The transitions correspond to these edges:
    /// - Trans1 corresponds to Edge 0 (V1 -> V2)
    /// - Trans2 corresponds to Edge 1 (V2 -> V3)
    /// - Trans3 corresponds to Edge 2 (V3 -> V1)
    ///
    /// MATERIAL INHERITANCE:
    ///
    /// BWMFace inherits from Face, which provides:
    /// - V1, V2, V3: The three vertices
    /// - Material: The surface material
    /// - Normal(): Calculates the triangle's normal vector
    /// - Area(): Calculates the triangle's area
    /// - Centre(): Gets the center point of the triangle
    /// - DetermineZ(x, y): Calculates height at a given (x, y) point
    ///
    /// The Material property is critical for walkability. When checking if a face is walkable,
    /// the code calls Material.Walkable() which checks if the material ID is in the walkable set.
    ///
    /// CRITICAL: MATERIAL PRESERVATION DURING TRANSFORMATIONS:
    ///
    /// When a BWM is transformed (flipped, rotated, or translated), the Material property MUST
    /// be preserved. The BWM.Flip(), BWM.Rotate(), and BWM.Translate() methods only modify
    /// vertices, not materials, so materials are automatically preserved.
    ///
    /// However, when creating deep copies of BWMs (like in IndoorMap.ProcessBwm), you MUST
    /// ensure that Material is copied: newFace.Material = face.Material
    ///
    /// If materials are not preserved, faces that should be walkable will become non-walkable,
    /// causing the bug where "levels/modules are NOT walkable despite having the right surface material."
    ///
    /// ORIGINAL IMPLEMENTATION:
    ///
    /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): BWM faces are stored in the BWM file format with:
    /// - Vertex indices (3 per face, pointing to vertex array)
    /// - Surface material (1 per face, uint32)
    /// - Transition indices (3 per face, for WOK area walkmeshes only)
    ///
    /// The original engine stores transitions as part of the face data structure, allowing
    /// the game to know which edges connect to other areas or have doors.
    /// </remarks>
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/bwm_data.py:1426-1531
    // Original: class BWMFace(Face, ComparableMixin)
    public class BWMFace : Face
    {
        /// <summary>
        /// Transition index for edge 0 (V1 -> V2). Null if no connection, otherwise the index of the connected room/area.
        /// </summary>
        public int? Trans1 { get; set; }

        /// <summary>
        /// Transition index for edge 1 (V2 -> V3). Null if no connection, otherwise the index of the connected room/area.
        /// </summary>
        public int? Trans2 { get; set; }

        /// <summary>
        /// Transition index for edge 2 (V3 -> V1). Null if no connection, otherwise the index of the connected room/area.
        /// </summary>
        public int? Trans3 { get; set; }

        /// <summary>
        /// Creates a new BWMFace with the specified vertices.
        /// Material defaults to Undefined (0) if not specified.
        /// All transitions are initialized to null (no connections).
        /// </summary>
        /// <param name="v1">First vertex of the triangle</param>
        /// <param name="v2">Second vertex of the triangle</param>
        /// <param name="v3">Third vertex of the triangle</param>
        public BWMFace(Vector3 v1, Vector3 v2, Vector3 v3) : base(v1, v2, v3)
        {
            Trans1 = null;
            Trans2 = null;
            Trans3 = null;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is BWMFace other))
            {
                return false;
            }
            bool parentEq = base.Equals(other);
            return parentEq && Trans1 == other.Trans1 && Trans2 == other.Trans2 && Trans3 == other.Trans3;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(base.GetHashCode(), Trans1, Trans2, Trans3);
        }
    }
}

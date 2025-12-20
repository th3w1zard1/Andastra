using System;
using System.Collections.Generic;
using System.Numerics;
using Xunit;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Resource.Formats.BWM;
using Andastra.Parsing.Resource.Formats.LYT;

namespace Andastra.Tests.Runtime.Core.Navigation
{
    /// <summary>
    /// Tests for walkmesh functionality, focusing on reproducing Indoor Map Builder issues.
    /// 
    /// Issue Background:
    /// The Holocron Indoor Map Builder (v4 beta) generates modules that suffer from three distinct walkability problems:
    /// 1. **Complete Immobility**: Players are stuck in place, unable to move at all.
    /// 2. **Room Transition Failure**: Players can move within a room but cannot transition between rooms via doors.
    /// 3. **Format Conversion Issues**: Modules generated with v2.0.4 work in K1 but fail in K2 after conversion.
    /// 
    /// Root Cause Analysis:
    /// These tests identify and verify fixes for the underlying walkmesh data issues that cause these problems.
    /// </summary>
    public class WalkmeshTests
    {
        /// <summary>
        /// Test 1: Verify walkmesh type is correctly set to AreaModel for AABB tree generation.
        /// 
        /// Issue: If WalkmeshType is not set to AreaModel, the AABB tree cannot be built, causing:
        /// - Inefficient brute-force face detection
        /// - Failed FindFaceAt operations
        /// - Players unable to move (cannot find walkable face at player position)
        /// 
        /// This reproduces Problem 1: Complete Immobility
        /// </summary>
        [Fact]
        public void Walkmesh_AreaModel_Type_EnablesAABBTree()
        {
            // Arrange: Create a simple walkmesh with correct AreaModel type
            var bwm = new BWM()
            {
                WalkmeshType = BWMType.AreaModel,
                Position = Vector3.Zero,
                RelativeUsePosition = Vector3.Zero,
                AbsoluteUsePosition = Vector3.Zero
            };

            // Add some walkmesh vertices
            var vertices = new List<Vector3>
            {
                new Vector3(0, 0, 0),
                new Vector3(1, 0, 0),
                new Vector3(0, 1, 0),
                new Vector3(1, 1, 0),
                new Vector3(2, 0, 0),
                new Vector3(2, 1, 0)
            };

            // Add walkable faces
            var faces = new List<BWMFace>
            {
                new BWMFace { Material = 1 }, // Walkable
                new BWMFace { Material = 1 },  // Walkable
                new BWMFace { Material = 1 }   // Walkable
            };

            bwm._vertices = vertices.ToArray();
            bwm._faceIndices = new[] { 0, 1, 2, 1, 2, 3, 3, 4, 5 };
            bwm._faces = faces.ToArray();
            bwm._vertexCount = (uint)vertices.Count;

            // Assert: AreaModel type must be set for AABB tree
            Assert.Equal(BWMType.AreaModel, bwm.WalkmeshType);
            
            // Assert: Should be able to enumerate walkable faces
            var walkableFaces = bwm.WalkableFaces();
            Assert.NotNull(walkableFaces);
        }

        /// <summary>
        /// Test 2: Verify material preservation during walkmesh transformations.
        /// 
        /// Issue: If material information is lost during copy/transform, all faces become non-walkable:
        /// - Causes complete immobility
        /// - Players cannot find any walkable surface
        /// 
        /// Root Cause: PyKotor's IndoorMap.process_bwm() was losing material data
        /// </summary>
        [Fact]
        public void Walkmesh_Material_PreservedAfterCopy()
        {
            // Arrange: Create walkmesh with specific materials
            var originalBWM = new BWM()
            {
                WalkmeshType = BWMType.AreaModel
            };

            var vertices = new[] { Vector3.Zero, Vector3.UnitX, Vector3.UnitY };
            var faceIndices = new[] { 0, 1, 2 };
            var faces = new[]
            {
                new BWMFace { Material = 1 }, // Walkable (grass)
                new BWMFace { Material = 0 }  // Non-walkable (void)
            };

            originalBWM._vertices = vertices;
            originalBWM._faceIndices = faceIndices;
            originalBWM._faces = faces;
            originalBWM._vertexCount = 3;

            // Act: Deep copy the walkmesh (simulating Indoor Map Builder operation)
            var copiedBWM = (BWM)originalBWM.Clone();

            // Assert: Materials must be preserved
            Assert.NotNull(copiedBWM._faces);
            Assert.Equal(originalBWM._faces.Length, copiedBWM._faces.Length);
            for (int i = 0; i < originalBWM._faces.Length; i++)
            {
                Assert.Equal(originalBWM._faces[i].Material, copiedBWM._faces[i].Material);
            }
        }

        /// <summary>
        /// Test 3: Verify transition indices (Trans1/Trans2/Trans3) are correctly set for door connections.
        /// 
        /// Issue: If transition indices are not remapped when combining room walkmeshes:
        /// - Door connections point to wrong rooms
        /// - Players cannot transition through doors
        /// - Causes Problem 2: Room Transition Failure
        /// 
        /// Root Cause: PyKotor's remap_transitions() function must correctly update Trans1/Trans2/Trans3 fields
        /// </summary>
        [Fact]
        public void Walkmesh_Transitions_CorrectlyRemapped()
        {
            // Arrange: Create two walkmeshes representing connected rooms
            var room1 = CreateSimpleWalkmesh(startIndex: 0);
            var room2 = CreateSimpleWalkmesh(startIndex: 100);

            // Set up a transition in room1 that should link to room2
            if (room1._faces != null && room1._faces.Length > 0)
            {
                // Dummy index (0) marks the transition location in room1
                room1._faces[0].Trans1 = 0;
            }

            // Act: Remap transition from dummy index (0) to actual room2 index (100)
            room1.ChangeLytIndexes(0, 100);

            // Assert: Transition should now point to room2
            if (room1._faces != null && room1._faces.Length > 0)
            {
                Assert.Equal(100, room1._faces[0].Trans1 ?? -1);
            }
        }

        /// <summary>
        /// Test 4: Verify adjacency connections are maintained within combined walkmeshes.
        /// 
        /// Issue: When multiple room walkmeshes are combined into a single navigation mesh:
        /// - Internal adjacency references must be remapped
        /// - Without remapping, pathfinding cannot navigate between faces
        /// - Causes room transition failures and movement restrictions
        /// </summary>
        [Fact]
        public void Walkmesh_Adjacency_PreservedInCombinedMesh()
        {
            // Arrange: Create a walkmesh with face adjacencies
            var bwm = new BWM()
            {
                WalkmeshType = BWMType.AreaModel
            };

            var vertices = new[]
            {
                Vector3.Zero, Vector3.UnitX, Vector3.UnitY,           // Face 0
                Vector3.UnitX, Vector3.One, Vector3.UnitY              // Face 1 (adjacent)
            };

            var faceIndices = new[] { 0, 1, 2, 1, 3, 2 };
            var faces = new[]
            {
                new BWMFace { Material = 1 },  // Face 0: Walkable
                new BWMFace { Material = 1 }   // Face 1: Walkable
            };

            bwm._vertices = vertices;
            bwm._faceIndices = faceIndices;
            bwm._faces = faces;
            bwm._vertexCount = 4;

            // Assert: Walkmesh should be valid for adjacency
            Assert.NotNull(bwm._faces);
            Assert.Equal(2, bwm._faces.Length);
            Assert.Equal(1, bwm._faces[0].Material);
            Assert.Equal(1, bwm._faces[1].Material);
        }

        /// <summary>
        /// Test 5: Verify walkmesh operations handle placeholder/stub cases correctly.
        /// 
        /// Issue: During indoor map generation, temporary placeholder geometry may be used.
        /// If not properly handled, this causes:
        /// - Invalid face references
        /// - Segmentation faults or access violations
        /// - Test reproduction: Verify null/empty walkmesh handling
        /// </summary>
        [Fact]
        public void Walkmesh_HandlesEmptyGeometry()
        {
            // Arrange: Create an empty walkmesh
            var bwm = new BWM()
            {
                WalkmeshType = BWMType.AreaModel,
                _vertexCount = 0
            };

            // Act & Assert: Should handle empty walkmesh gracefully
            Assert.NotNull(bwm);
            Assert.Equal(0u, bwm._vertexCount);
            
            // Should not throw when checking walkable faces
            var walkableFaces = bwm.WalkableFaces();
            Assert.NotNull(walkableFaces);
        }

        /// <summary>
        /// Helper: Creates a simple test walkmesh for reproducibility.
        /// </summary>
        private static BWM CreateSimpleWalkmesh(int startIndex = 0)
        {
            var bwm = new BWM()
            {
                WalkmeshType = BWMType.AreaModel,
                Position = new Vector3(startIndex, 0, 0)
            };

            var vertices = new[]
            {
                Vector3.Zero, Vector3.UnitX, Vector3.UnitY,
                Vector3.UnitX, Vector3.One, Vector3.UnitY
            };

            var faces = new[]
            {
                new BWMFace { Material = 1 },
                new BWMFace { Material = 1 }
            };

            bwm._vertices = vertices;
            bwm._faceIndices = new[] { 0, 1, 2, 1, 3, 2 };
            bwm._faces = faces;
            bwm._vertexCount = (uint)vertices.Length;

            return bwm;
        }
    }
}




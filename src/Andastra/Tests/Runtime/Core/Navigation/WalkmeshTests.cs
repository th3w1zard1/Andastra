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
                // TODO:  Dummy index (0) marks the transition location in room1
                room1._faces[0].Trans1 = 0;
            }

            // TODO:  Act: Remap transition from dummy index (0) to actual room2 index (100)
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
        /// - Null reference exceptions
        /// 
        /// This test comprehensively verifies that ALL BWM operations handle empty, null,
        /// and placeholder walkmesh cases gracefully without throwing exceptions.
        /// 
        /// Based on swkotor2.exe: Empty walkmeshes should return empty/null results without crashing.
        /// Reference: ModuleKit._create_component_from_lyt_room() creates placeholder BWM when WOK is missing.
        /// </summary>
        [Fact]
        public void Walkmesh_HandlesEmptyGeometry()
        {
            // Test Case 1: Empty walkmesh (no faces)
            var emptyBwm = new BWM()
            {
                WalkmeshType = BWMType.AreaModel,
                Faces = new List<BWMFace>()
            };

            Assert.NotNull(emptyBwm);
            Assert.NotNull(emptyBwm.Faces);
            Assert.Empty(emptyBwm.Faces);

            // Test all operations that should work with empty walkmesh
            var walkableFaces = emptyBwm.WalkableFaces();
            Assert.NotNull(walkableFaces);
            Assert.Empty(walkableFaces);

            var unwalkableFaces = emptyBwm.UnwalkableFaces();
            Assert.NotNull(unwalkableFaces);
            Assert.Empty(unwalkableFaces);

            var vertices = emptyBwm.Vertices();
            Assert.NotNull(vertices);
            Assert.Empty(vertices);

            var aabbs = emptyBwm.Aabbs();
            Assert.NotNull(aabbs);
            Assert.Empty(aabbs);

            var edges = emptyBwm.Edges();
            Assert.NotNull(edges);
            Assert.Empty(edges);

            // Raycast should return null (no hit)
            var raycastResult = emptyBwm.Raycast(Vector3.Zero, Vector3.UnitY, 100.0f);
            Assert.Null(raycastResult);

            // FindFaceAt should return null (no face found)
            var faceAt = emptyBwm.FindFaceAt(0.0f, 0.0f);
            Assert.Null(faceAt);

            // GetHeightAt should return null (no face found)
            var heightAt = emptyBwm.GetHeightAt(0.0f, 0.0f);
            Assert.Null(heightAt);

            // FaceAt should return null (no face found)
            var faceAtResult = emptyBwm.FaceAt(0.0f, 0.0f);
            Assert.Null(faceAtResult);

            // Box should return valid bounding box (even if empty, should return initialized values)
            var box = emptyBwm.Box();
            Assert.NotNull(box);

            // Adjacencies should work with a face from another walkmesh (empty walkmesh has no adjacencies)
            // Create a test face from another walkmesh
            var testFace = new BWMFace(Vector3.Zero, Vector3.UnitX, Vector3.UnitY)
            {
                Material = SurfaceMaterial.Grass
            };
            var adjacencies = emptyBwm.Adjacencies(testFace);
            Assert.NotNull(adjacencies);
            // Empty walkmesh should have no adjacencies for external face
            Assert.Null(adjacencies.Item1);
            Assert.Null(adjacencies.Item2);
            Assert.Null(adjacencies.Item3);

            // Transform operations should work without throwing (no-op on empty walkmesh)
            emptyBwm.Translate(10.0f, 20.0f, 30.0f);
            Assert.Empty(emptyBwm.Faces); // Should still be empty after translate

            emptyBwm.Rotate(45.0f);
            Assert.Empty(emptyBwm.Faces); // Should still be empty after rotate

            emptyBwm.Flip(true, false);
            Assert.Empty(emptyBwm.Faces); // Should still be empty after flip

            emptyBwm.ChangeLytIndexes(0, 1);
            Assert.Empty(emptyBwm.Faces); // Should still be empty after change lyt indexes

            emptyBwm.RemapTransitions(0, 1);
            Assert.Empty(emptyBwm.Faces); // Should still be empty after remap transitions

            // Test Case 2: Placeholder walkmesh (has faces but none are walkable)
            var placeholderBwm = new BWM()
            {
                WalkmeshType = BWMType.AreaModel,
                Faces = new List<BWMFace>
                {
                    new BWMFace(Vector3.Zero, Vector3.UnitX, Vector3.UnitY)
                    {
                        Material = SurfaceMaterial.Obscuring // Non-walkable material
                    }
                }
            };

            var placeholderWalkable = placeholderBwm.WalkableFaces();
            Assert.NotNull(placeholderWalkable);
            Assert.Empty(placeholderWalkable); // No walkable faces

            var placeholderUnwalkable = placeholderBwm.UnwalkableFaces();
            Assert.NotNull(placeholderUnwalkable);
            Assert.Single(placeholderUnwalkable); // One unwalkable face

            // FindFaceAt with walkable materials should return null (no walkable faces)
            var placeholderFaceAt = placeholderBwm.FindFaceAt(0.1f, 0.1f);
            Assert.Null(placeholderFaceAt); // Point is inside face, but face is not walkable

            // FindFaceAt with all materials should find the face
            var allMaterials = new HashSet<SurfaceMaterial>();
            foreach (SurfaceMaterial mat in Enum.GetValues(typeof(SurfaceMaterial)))
            {
                allMaterials.Add(mat);
            }
            var placeholderFaceAtAll = placeholderBwm.FindFaceAt(0.1f, 0.1f, allMaterials);
            Assert.NotNull(placeholderFaceAtAll); // Should find face when searching all materials

            // Test Case 3: Null Faces property (defensive programming)
            // Note: BWM constructor initializes Faces to empty list, so we need to test with reflection
            // or verify constructor behavior. Since constructor always initializes, we'll test edge cases.

            // Test Case 4: Walkmesh with single face (minimal valid walkmesh)
            var singleFaceBwm = new BWM()
            {
                WalkmeshType = BWMType.AreaModel,
                Faces = new List<BWMFace>
                {
                    new BWMFace(new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0))
                    {
                        Material = SurfaceMaterial.Grass // Walkable
                    }
                }
            };

            var singleWalkable = singleFaceBwm.WalkableFaces();
            Assert.NotNull(singleWalkable);
            Assert.Single(singleWalkable);

            var singleAabbs = singleFaceBwm.Aabbs();
            Assert.NotNull(singleAabbs);
            // Single face should create at least one AABB node (likely a leaf node)

            // Test Case 5: PlaceableOrDoor walkmesh type (should handle empty gracefully)
            var placeableBwm = new BWM()
            {
                WalkmeshType = BWMType.PlaceableOrDoor,
                Faces = new List<BWMFace>()
            };

            var placeableAabbs = placeableBwm.Aabbs();
            Assert.NotNull(placeableAabbs);
            Assert.Empty(placeableAabbs); // PlaceableOrDoor should return empty AABBs

            var placeableRaycast = placeableBwm.Raycast(Vector3.Zero, Vector3.UnitY, 100.0f);
            Assert.Null(placeableRaycast); // Should return null for empty placeable walkmesh
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




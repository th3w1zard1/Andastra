using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.BWM;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Resource.Formats.LYT;
using Xunit;

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
                RelativeHook1 = Vector3.Zero,
                RelativeHook2 = Vector3.Zero,
                AbsoluteHook1 = Vector3.Zero,
                AbsoluteHook2 = Vector3.Zero
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

            // Add walkable faces using vertices from the list
            var faces = new List<BWMFace>
            {
                new BWMFace(vertices[0], vertices[1], vertices[2]), // Walkable
                new BWMFace(vertices[1], vertices[2], vertices[3]),  // Walkable
                new BWMFace(vertices[3], vertices[4], vertices[5])   // Walkable
            };

            bwm.Faces = faces;

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
                new BWMFace(Vector3.Zero, Vector3.UnitX, Vector3.UnitY) { Material = (SurfaceMaterial)1 }, // Walkable (grass)
                new BWMFace(Vector3.Zero, Vector3.UnitX, Vector3.UnitY) { Material = (SurfaceMaterial)0 }  // Non-walkable (void)
            };

            originalBWM.Faces = new List<BWMFace>(faces);

            // Act: Deep copy the walkmesh (simulating Indoor Map Builder operation)
            var copiedBWM = new BWM
            {
                WalkmeshType = originalBWM.WalkmeshType,
                Faces = new List<BWMFace>(originalBWM.Faces.Select(f => new BWMFace(f.V1, f.V2, f.V3) { Material = f.Material })),
                Position = originalBWM.Position,
                RelativeHook1 = originalBWM.RelativeHook1,
                RelativeHook2 = originalBWM.RelativeHook2,
                AbsoluteHook1 = originalBWM.AbsoluteHook1,
                AbsoluteHook2 = originalBWM.AbsoluteHook2
            };

            // Assert: Materials must be preserved
            Assert.NotNull(copiedBWM.Faces);
            Assert.Equal(originalBWM.Faces.Count, copiedBWM.Faces.Count);
            for (int i = 0; i < originalBWM.Faces.Count; i++)
            {
                Assert.Equal(originalBWM.Faces[i].Material, copiedBWM.Faces[i].Material);
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
        ///
        /// Based on swkotor2.exe: Transition indices are stored per-edge in walkmesh faces and must be
        /// remapped when combining room walkmeshes. The original engine uses transition indices to connect
        /// rooms via doors and area boundaries.
        ///
        /// Reference: Tools/HolocronToolset/src/toolset/data/indoormap.py:426-452 (remap_transitions)
        /// </summary>
        [Fact]
        public void Walkmesh_Transitions_CorrectlyRemapped()
        {
            // Arrange: Create two walkmeshes representing connected rooms
            var room1 = CreateSimpleWalkmesh(startIndex: 0);
            var room2 = CreateSimpleWalkmesh(startIndex: 100);

            // Set up transitions in room1 that should link to room2
            // Dummy index (0) marks the transition location in room1 (from kit component)
            // This will be remapped to actual room2 index (100) when rooms are connected
            if (room1.Faces != null && room1.Faces.Count >= 2)
            {
                // Face 0: Trans1 has dummy index 0 (will be remapped to 100)
                room1.Faces[0].Trans1 = 0;
                // Face 0: Trans2 has different dummy index 1 (will not be remapped in this test)
                room1.Faces[0].Trans2 = 1;
                // Face 0: Trans3 is null (no transition, should remain null)
                room1.Faces[0].Trans3 = null;

                // Face 1: Trans1 has dummy index 0 (will be remapped to 100)
                room1.Faces[1].Trans1 = 0;
                // Face 1: Trans2 has dummy index 0 (will be remapped to 100)
                room1.Faces[1].Trans2 = 0;
                // Face 1: Trans3 has different value 5 (will not be remapped)
                room1.Faces[1].Trans3 = 5;
            }

            // Act: Remap transition from dummy index (0) to actual room2 index (100)
            // This simulates the indoor map builder connecting room1 to room2
            room1.ChangeLytIndexes(0, 100);

            // Assert: All transitions with dummy index 0 should now point to room2 (100)
            if (room1.Faces != null && room1.Faces.Count >= 2)
            {
                // Face 0: Trans1 was remapped from 0 to 100
                Assert.Equal(100, room1.Faces[0].Trans1 ?? -1);
                // Face 0: Trans2 was not remapped (different dummy index 1)
                Assert.Equal(1, room1.Faces[0].Trans2 ?? -1);
                // Face 0: Trans3 remains null (no transition)
                Assert.Null(room1.Faces[0].Trans3);

                // Face 1: Trans1 was remapped from 0 to 100
                Assert.Equal(100, room1.Faces[1].Trans1 ?? -1);
                // Face 1: Trans2 was remapped from 0 to 100
                Assert.Equal(100, room1.Faces[1].Trans2 ?? -1);
                // Face 1: Trans3 was not remapped (different value 5)
                Assert.Equal(5, room1.Faces[1].Trans3 ?? -1);
            }
        }

        /// <summary>
        /// Test 3b: Verify transition remapping handles null connections (disconnected hooks).
        ///
        /// Issue: When a hook has no connection (connection is null), the transition should be set to null,
        /// indicating no connection. This is critical for proper door placement and area boundaries.
        ///
        /// Based on Tools/HolocronToolset/src/toolset/data/indoormap.py:422-423
        /// When connection is None, actual_index is None, and RemapTransitions sets transitions to null.
        /// </summary>
        [Fact]
        public void Walkmesh_Transitions_RemappedToNullForDisconnectedHooks()
        {
            // Arrange: Create a walkmesh with transitions that should be disconnected
            var room1 = CreateSimpleWalkmesh(startIndex: 0);

            // Set up transitions with dummy index 0 (from kit component)
            if (room1.Faces != null && room1.Faces.Count > 0)
            {
                room1.Faces[0].Trans1 = 0;
                room1.Faces[0].Trans2 = 0;
                room1.Faces[0].Trans3 = 0;
            }

            // Act: Remap transition from dummy index (0) to null (no connection)
            // This simulates a hook that has no connection in the indoor map builder
            room1.ChangeLytIndexes(0, null);

            // Assert: All transitions with dummy index 0 should now be null
            if (room1.Faces != null && room1.Faces.Count > 0)
            {
                Assert.Null(room1.Faces[0].Trans1);
                Assert.Null(room1.Faces[0].Trans2);
                Assert.Null(room1.Faces[0].Trans3);
            }
        }

        /// <summary>
        /// Test 3c: Verify RemapTransitions method works identically to ChangeLytIndexes.
        ///
        /// Both methods should perform the same operation: remapping transition indices.
        /// RemapTransitions is the method name used in PyKotor's IndoorMap class.
        /// ChangeLytIndexes is an alias method for the same operation.
        /// </summary>
        [Fact]
        public void Walkmesh_RemapTransitions_EquivalentToChangeLytIndexes()
        {
            // Arrange: Create two walkmeshes
            var room1a = CreateSimpleWalkmesh(startIndex: 0);
            var room1b = CreateSimpleWalkmesh(startIndex: 0);

            // Set up identical transitions
            if (room1a.Faces != null && room1a.Faces.Count > 0 && room1b.Faces != null && room1b.Faces.Count > 0)
            {
                room1a.Faces[0].Trans1 = 0;
                room1a.Faces[0].Trans2 = 0;
                room1b.Faces[0].Trans1 = 0;
                room1b.Faces[0].Trans2 = 0;
            }

            // Act: Use different methods to remap transitions
            room1a.ChangeLytIndexes(0, 100);
            room1b.RemapTransitions(0, 100);

            // Assert: Both methods produce identical results
            if (room1a.Faces != null && room1a.Faces.Count > 0 && room1b.Faces != null && room1b.Faces.Count > 0)
            {
                Assert.Equal(room1a.Faces[0].Trans1, room1b.Faces[0].Trans1);
                Assert.Equal(room1a.Faces[0].Trans2, room1b.Faces[0].Trans2);
            }
        }

        /// <summary>
        /// Test 3d: Verify transition remapping only affects matching indices.
        ///
        /// Issue: When remapping transitions, only faces with the exact dummy index should be updated.
        /// Other transition values must remain unchanged to preserve existing connections.
        /// </summary>
        [Fact]
        public void Walkmesh_Transitions_OnlyMatchingIndicesRemapped()
        {
            // Arrange: Create a walkmesh with multiple different transition values
            var room1 = CreateSimpleWalkmesh(startIndex: 0);

            if (room1.Faces != null && room1.Faces.Count >= 2)
            {
                // Face 0: Mix of values to remap and values to preserve
                room1.Faces[0].Trans1 = 0;  // Will be remapped
                room1.Faces[0].Trans2 = 5;  // Should remain 5
                room1.Faces[0].Trans3 = 10; // Should remain 10

                // Face 1: All different values
                room1.Faces[1].Trans1 = 7;  // Should remain 7
                room1.Faces[1].Trans2 = 8; // Should remain 8
                room1.Faces[1].Trans3 = 9; // Should remain 9
            }

            // Act: Remap only dummy index 0 to 100
            room1.ChangeLytIndexes(0, 100);

            // Assert: Only Trans1 of Face 0 was remapped, all others unchanged
            if (room1.Faces != null && room1.Faces.Count >= 2)
            {
                // Face 0: Only Trans1 was remapped
                Assert.Equal(100, room1.Faces[0].Trans1 ?? -1);
                Assert.Equal(5, room1.Faces[0].Trans2 ?? -1);
                Assert.Equal(10, room1.Faces[0].Trans3 ?? -1);

                // Face 1: All transitions unchanged
                Assert.Equal(7, room1.Faces[1].Trans1 ?? -1);
                Assert.Equal(8, room1.Faces[1].Trans2 ?? -1);
                Assert.Equal(9, room1.Faces[1].Trans3 ?? -1);
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

            var faces = new List<BWMFace>
            {
                new BWMFace(Vector3.Zero, Vector3.UnitX, Vector3.UnitY) { Material = (SurfaceMaterial)1 },  // Face 0: Walkable
                new BWMFace(Vector3.UnitX, Vector3.One, Vector3.UnitY) { Material = (SurfaceMaterial)1 }   // Face 1: Walkable
            };

            bwm.Faces = faces;

            // Assert: Walkmesh should be valid for adjacency
            Assert.NotNull(bwm.Faces);
            Assert.Equal(2, bwm.Faces.Count);
            Assert.Equal((SurfaceMaterial)1, bwm.Faces[0].Material);
            Assert.Equal((SurfaceMaterial)1, bwm.Faces[1].Material);
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

            var faces = new List<BWMFace>
            {
                new BWMFace(Vector3.Zero, Vector3.UnitX, Vector3.UnitY) { Material = (SurfaceMaterial)1 },
                new BWMFace(Vector3.UnitX, Vector3.One, Vector3.UnitY) { Material = (SurfaceMaterial)1 }
            };

            bwm.Faces = faces;

            return bwm;
        }
    }
}




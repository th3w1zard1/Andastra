using System;
using System.Collections.Generic;
using System.Numerics;
using BioWare.NET;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.BWM;
using BioWare.NET.Extract.Installation;
using BioWare.NET.Resource;
using Andastra.Runtime.Core.Module;
using Andastra.Game.Games.Eclipse;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Eclipse.Loading
{
    /// <summary>
    /// Factory for creating EclipseNavigationMesh from BWM walkmesh data.
    /// </summary>
    /// <remarks>
    /// Eclipse Navigation Mesh Factory:
    /// - Based on daorigins.exe/DragonAge2.exe/ navigation systems
    /// - Eclipse uses BWM format similar to Odyssey but with additional features
    /// - Supports dynamic obstacles, destructible terrain, and multi-level navigation
    /// - Physics-aware navigation with collision avoidance
    ///
    /// Eclipse-specific features:
    /// - Multi-level navigation surfaces (ground, platforms, elevated surfaces)
    /// - Dynamic obstacle support (added at runtime)
    /// - Destructible terrain modifications (applied at runtime)
    /// - Physics-aware navigation with collision avoidance
    ///
    /// BWM file format (same as Odyssey):
    /// - Header: "BWM V1.0" signature (8 bytes)
    /// - Walkmesh type: 0 = PWK/DWK (placeable/door), 1 = WOK (area walkmesh)
    /// - Vertices: Array of float3 (x, y, z) positions
    /// - Faces: Array of uint32 triplets (vertex indices per triangle)
    /// - Materials: Array of uint32 (SurfaceMaterial ID per face)
    /// - Adjacency: Array of int32 triplets (face/edge pairs, -1 = no neighbor)
    /// - AABB tree: Spatial acceleration structure for efficient queries
    ///
    /// Based on BWM file format documentation:
    /// - vendor/PyKotor/wiki/BWM-File-Format.md
    /// </remarks>
    public class EclipseNavigationMeshFactory
    {
        /// <summary>
        /// Creates a combined navigation mesh from all room walkmeshes in a module.
        /// </summary>
        /// <param name="module">The module containing the area and walkmesh resources.</param>
        /// <param name="rooms">The list of room information from the LYT file (if available).</param>
        /// <returns>An EclipseNavigationMesh combining all room walkmeshes, or null if no walkmeshes found.</returns>
        [CanBeNull]
        public EclipseNavigationMesh CreateFromModule(Module module, List<RoomInfo> rooms)
        {
            if (module == null || rooms == null || rooms.Count == 0)
            {
                return null;
            }

            // Collect all walkmeshes from all rooms
            var roomWalkmeshes = new List<EclipseNavigationMesh>();

            foreach (RoomInfo room in rooms)
            {
                if (string.IsNullOrEmpty(room.ModelName))
                {
                    continue;
                }

                try
                {
                    // Load WOK resource for the room model
                    ResourceResult wokResource = module.Installation.Resource(room.ModelName, ResourceType.WOK,
                        new[] { SearchLocation.CHITIN, SearchLocation.CUSTOM_MODULES });

                    if (wokResource?.Data != null)
                    {
                        BWM bwm = BWMAuto.ReadBwm(wokResource.Data);
                        if (bwm != null)
                        {
                            // Convert BWM to EclipseNavigationMesh, applying room position as offset
                            EclipseNavigationMesh roomNavMesh = BwmToEclipseNavigationMeshConverter.ConvertWithOffset(bwm, room.Position);
                            roomWalkmeshes.Add(roomNavMesh);
                        }
                    }
                }
                catch (Exception)
                {
                    // Log error, but continue with other rooms
                }
            }

            if (roomWalkmeshes.Count == 0)
            {
                return null;
            }

            // Merge all room walkmeshes into a single navigation mesh
            return BwmToEclipseNavigationMeshConverter.Merge(roomWalkmeshes);
        }

        /// <summary>
        /// Creates an EclipseNavigationMesh from a single BWM file.
        /// </summary>
        /// <param name="bwm">The BWM walkmesh data.</param>
        /// <returns>An EclipseNavigationMesh created from the BWM data.</returns>
        public EclipseNavigationMesh CreateFromBwm(BWM bwm)
        {
            if (bwm == null)
            {
                return new EclipseNavigationMesh();
            }

            return BwmToEclipseNavigationMeshConverter.Convert(bwm);
        }

        /// <summary>
        /// Creates an EclipseNavigationMesh from a single BWM file with position offset.
        /// </summary>
        /// <param name="bwm">The BWM walkmesh data.</param>
        /// <param name="offset">The position offset to apply to all vertices.</param>
        /// <returns>An EclipseNavigationMesh created from the BWM data with offset applied.</returns>
        public EclipseNavigationMesh CreateFromBwmWithOffset(BWM bwm, Vector3 offset)
        {
            if (bwm == null)
            {
                return new EclipseNavigationMesh();
            }

            return BwmToEclipseNavigationMeshConverter.ConvertWithOffset(bwm, offset);
        }
    }
}


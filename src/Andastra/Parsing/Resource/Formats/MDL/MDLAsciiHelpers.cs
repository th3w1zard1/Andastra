using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.MDL;
using Andastra.Parsing.Formats.MDLData;

namespace Andastra.Parsing.Formats.MDL
{
    // Helper functions for MDL ASCII format (matching PyKotor io_mdl_ascii.py)
    internal static class MDLAsciiHelpers
    {
        private const int FACE_SURFACE_MASK = 0x1F;
        private const int FACE_SMOOTH_SHIFT = 5;

        // Node type constants matching mdlops (vendor/mdlops/MDLOpsM.pm:313-323)
        internal const int NODE_DUMMY = 1;
        internal const int NODE_LIGHT = 3;
        internal const int NODE_EMITTER = 5;
        internal const int NODE_REFERENCE = 17;
        internal const int NODE_TRIMESH = 33;
        internal const int NODE_SKIN = 97;
        internal const int NODE_DANGLYMESH = 289;
        internal const int NODE_AABB = 545;
        internal const int NODE_SABER = 2081;

        // Node flag constants matching mdlops (vendor/mdlops/MDLOpsM.pm:301-311)
        internal const int NODE_HAS_HEADER = 0x0001;
        internal const int NODE_HAS_LIGHT = 0x0002;
        internal const int NODE_HAS_EMITTER = 0x0004;
        internal const int NODE_HAS_REFERENCE = 0x0010;
        internal const int NODE_HAS_MESH = 0x0020;
        internal const int NODE_HAS_SKIN = 0x0040;
        internal const int NODE_HAS_DANGLY = 0x0100;
        internal const int NODE_HAS_AABB = 0x0200;
        internal const int NODE_HAS_SABER = 0x0800;

        /// <summary>
        /// Return (surface_material, smoothing_group) from packed face material flags.
        /// Binary MDL packs multiple flags in the 32-bit material field.
        /// Reference: vendor/mdlops/MDLOpsM.pm:2254-2256
        /// </summary>
        internal static Tuple<int, int> UnpackFaceMaterial(MDLFace face)
        {
            // C# MDLFace uses SurfaceMaterial enum, so we need to convert it to int
            int materialInt = (int)face.Material;
            int smoothing = face.SmoothingGroup;
            if (smoothing == 0)
            {
                smoothing = materialInt >> FACE_SMOOTH_SHIFT;
            }
            int surface = materialInt & FACE_SURFACE_MASK;
            return Tuple.Create(surface, smoothing);
        }

        /// <summary>
        /// Pack surface material and smoothing group into a single integer.
        /// </summary>
        internal static int PackFaceMaterial(int surfaceMaterial, int smoothingGroup)
        {
            return (smoothingGroup << FACE_SMOOTH_SHIFT) | (surfaceMaterial & FACE_SURFACE_MASK);
        }

        /// <summary>
        /// Convert angle-axis to quaternion (x, y, z, w).
        /// Reference: vendor/mdlops/MDLOpsM.pm:3718-3728
        /// </summary>
        internal static Vector4 AngleAxisToQuaternion(float x, float y, float z, float angle)
        {
            float sinA = (float)Math.Sin(angle / 2.0f);
            return new Vector4(x * sinA, y * sinA, z * sinA, (float)Math.Cos(angle / 2.0f));
        }

        /// <summary>
        /// Normalize a 3D vector.
        /// Reference: vendor/mdlops/MDLOpsM.pm:3623-3653
        /// </summary>
        internal static Vector3 NormalizeVector(Vector3 vec)
        {
            float norm = (float)Math.Sqrt(vec.X * vec.X + vec.Y * vec.Y + vec.Z * vec.Z);
            if (norm > 0)
            {
                return new Vector3(vec.X / norm, vec.Y / norm, vec.Z / norm);
            }
            return Vector3.Zero;
        }

        /// <summary>
        /// Get node flags based on attached data.
        /// </summary>
        internal static MDLNodeFlags GetNodeFlags(MDLNode node)
        {
            MDLNodeFlags flags = MDLNodeFlags.HEADER;
            if (node.Light != null)
            {
                flags |= MDLNodeFlags.LIGHT;
            }
            if (node.Emitter != null)
            {
                flags |= MDLNodeFlags.EMITTER;
            }
            if (node.Reference != null)
            {
                flags |= MDLNodeFlags.REFERENCE;
            }
            if (node.Mesh != null)
            {
                flags |= MDLNodeFlags.MESH;
            }
            if (node.Mesh?.Skin != null)
            {
                flags |= MDLNodeFlags.SKIN;
            }
            if (node.Mesh?.Dangly != null)
            {
                flags |= MDLNodeFlags.DANGLY;
            }
            if (node.Aabb != null || node.Walkmesh != null)
            {
                flags |= MDLNodeFlags.AABB;
            }
            if (node.Saber != null || node.Mesh?.Saber != null)
            {
                flags |= MDLNodeFlags.SABER;
            }
            return flags;
        }

        /// <summary>
        /// Get all nodes recursively from a node tree (including the root node itself).
        /// Equivalent to Python's all_nodes() function.
        /// </summary>
        internal static List<MDLNode> GetAllNodes(MDLNode root)
        {
            var result = new List<MDLNode>();
            GetAllNodesRecursive(root, result);
            return result;
        }

        private static void GetAllNodesRecursive(MDLNode node, List<MDLNode> result)
        {
            if (node == null) return;
            result.Add(node);
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    GetAllNodesRecursive(child, result);
                }
            }
        }
    }
}


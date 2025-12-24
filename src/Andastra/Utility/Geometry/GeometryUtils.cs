using System;
using System.Collections.Generic;
using System.Numerics;

namespace Andastra.Utility.Geometry
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/geometry_utils.py
    // Original: compute_per_vertex_tangent_space, determine_vertex_format_requirements
    public static class GeometryUtils
    {
        public static Dictionary<int, Tuple<Vector3, Vector3>> ComputePerVertexTangentSpace(IMdlMesh mesh)
        {
            Dictionary<int, List<Vector3>> vertexTangents = new Dictionary<int, List<Vector3>>();
            Dictionary<int, List<Vector3>> vertexBinormals = new Dictionary<int, List<Vector3>>();

            foreach (MdlFace face in mesh.Faces)
            {
                Vector3 v1 = mesh.VertexPositions[face.V1];
                Vector3 v2 = mesh.VertexPositions[face.V2];
                Vector3 v3 = mesh.VertexPositions[face.V3];

                if (face.V1 >= mesh.VertexUv.Count || face.V2 >= mesh.VertexUv.Count || face.V3 >= mesh.VertexUv.Count)
                {
                    continue;
                }

                Vector2 uv1 = mesh.VertexUv[face.V1];
                Vector2 uv2 = mesh.VertexUv[face.V2];
                Vector2 uv3 = mesh.VertexUv[face.V3];

                Vector3 faceNormal = CalculateFaceNormal(v1, v2, v3);
                Tuple<Vector3, Vector3> tspace = CalculateTangentSpace(v1, v2, v3, uv1, uv2, uv3, faceNormal);
                Vector3 tangent = tspace.Item1;
                Vector3 binormal = tspace.Item2;

                foreach (int idx in new[] { face.V1, face.V2, face.V3 })
                {
                    if (!vertexTangents.ContainsKey(idx))
                    {
                        vertexTangents[idx] = new List<Vector3>();
                        vertexBinormals[idx] = new List<Vector3>();
                    }

                    vertexTangents[idx].Add(tangent);
                    vertexBinormals[idx].Add(binormal);
                }
            }

            Dictionary<int, Tuple<Vector3, Vector3>> result = new Dictionary<int, Tuple<Vector3, Vector3>>();
            foreach (int idx in vertexTangents.Keys)
            {
                Vector3 avgTangent = Vector3.Zero;
                foreach (Vector3 t in vertexTangents[idx])
                {
                    avgTangent = new Vector3(avgTangent.X + t.X, avgTangent.Y + t.Y, avgTangent.Z + t.Z);
                }

                int count = vertexTangents[idx].Count;
                avgTangent = new Vector3(avgTangent.X / count, avgTangent.Y / count, avgTangent.Z / count);
                float tlen = (float)Math.Sqrt(avgTangent.X * avgTangent.X + avgTangent.Y * avgTangent.Y + avgTangent.Z * avgTangent.Z);
                if (tlen > 0)
                {
                    avgTangent = new Vector3(avgTangent.X / tlen, avgTangent.Y / tlen, avgTangent.Z / tlen);
                }

                Vector3 avgBinormal = Vector3.Zero;
                foreach (Vector3 b in vertexBinormals[idx])
                {
                    avgBinormal = new Vector3(avgBinormal.X + b.X, avgBinormal.Y + b.Y, avgBinormal.Z + b.Z);
                }
                avgBinormal = new Vector3(avgBinormal.X / count, avgBinormal.Y / count, avgBinormal.Z / count);
                float blen = (float)Math.Sqrt(avgBinormal.X * avgBinormal.X + avgBinormal.Y * avgBinormal.Y + avgBinormal.Z * avgBinormal.Z);
                if (blen > 0)
                {
                    avgBinormal = new Vector3(avgBinormal.X / blen, avgBinormal.Y / blen, avgBinormal.Z / blen);
                }

                result[idx] = Tuple.Create(avgTangent, avgBinormal);
            }

            return result;
        }

        public static Dictionary<string, bool> DetermineVertexFormatRequirements(IMdlMesh mesh)
        {
            return new Dictionary<string, bool>
            {
                { "has_normals", mesh.VertexNormals != null && mesh.VertexNormals.Count > 0 },
                { "has_tangent_space", mesh.VertexNormals != null && mesh.VertexNormals.Count > 0 && mesh.Faces.Count > 0 && mesh.VertexUv != null && mesh.VertexUv.Count > 0 },
                { "has_lightmap", mesh.HasLightmap && mesh.VertexUv2 != null && mesh.VertexUv2.Count > 0 },
                { "has_skinning", mesh.SkinPresent },
                { "has_uv2", mesh.VertexUv2 != null && mesh.VertexUv2.Count > 0 }
            };
        }

        private static Vector3 CalculateFaceNormal(Vector3 v1, Vector3 v2, Vector3 v3)
        {
            Vector3 edge1 = new Vector3(v2.X - v1.X, v2.Y - v1.Y, v2.Z - v1.Z);
            Vector3 edge2 = new Vector3(v3.X - v1.X, v3.Y - v1.Y, v3.Z - v1.Z);
            Vector3 normal = new Vector3(
                edge1.Y * edge2.Z - edge1.Z * edge2.Y,
                edge1.Z * edge2.X - edge1.X * edge2.Z,
                edge1.X * edge2.Y - edge1.Y * edge2.X
            );
            float len = (float)Math.Sqrt(normal.X * normal.X + normal.Y * normal.Y + normal.Z * normal.Z);
            if (len > 0)
            {
                normal = new Vector3(normal.X / len, normal.Y / len, normal.Z / len);
            }
            return normal;
        }

        private static Tuple<Vector3, Vector3> CalculateTangentSpace(
            Vector3 v1,
            Vector3 v2,
            Vector3 v3,
            Vector2 uv1,
            Vector2 uv2,
            Vector2 uv3,
            Vector3 faceNormal)
        {
            float x1 = v2.X - v1.X;
            float y1 = v2.Y - v1.Y;
            float z1 = v2.Z - v1.Z;

            float x2 = v3.X - v1.X;
            float y2 = v3.Y - v1.Y;
            float z2 = v3.Z - v1.Z;

            float s1 = uv2.X - uv1.X;
            float t1 = uv2.Y - uv1.Y;

            float s2 = uv3.X - uv1.X;
            float t2 = uv3.Y - uv1.Y;

            float r = (s1 * t2 - s2 * t1);
            if (Math.Abs(r) < 1e-8f)
            {
                r = 1.0f;
            }
            else
            {
                r = 1.0f / r;
            }

            Vector3 tangent = new Vector3(
                (t2 * x1 - t1 * x2) * r,
                (t2 * y1 - t1 * y2) * r,
                (t2 * z1 - t1 * z2) * r
            );

            Vector3 binormal = new Vector3(
                (s1 * x2 - s2 * x1) * r,
                (s1 * y2 - s2 * y1) * r,
                (s1 * z2 - s2 * z1) * r
            );

            // Orthogonalize tangent/binormal to the face normal
            tangent = Orthogonalize(tangent, faceNormal);
            binormal = Orthogonalize(binormal, faceNormal);

            return Tuple.Create(tangent, binormal);
        }

        private static Vector3 Orthogonalize(Vector3 v, Vector3 n)
        {
            float dot = v.X * n.X + v.Y * n.Y + v.Z * n.Z;
            Vector3 projected = new Vector3(
                v.X - dot * n.X,
                v.Y - dot * n.Y,
                v.Z - dot * n.Z
            );
            float len = (float)Math.Sqrt(projected.X * projected.X + projected.Y * projected.Y + projected.Z * projected.Z);
            if (len > 0)
            {
                projected = new Vector3(projected.X / len, projected.Y / len, projected.Z / len);
            }
            return projected;
        }
    }

    // Minimal mesh contracts to support GeometryUtils.
    public interface IMdlMesh
    {
        IList<MdlFace> Faces { get; }
        IList<Vector3> VertexPositions { get; }
        IList<Vector3> VertexNormals { get; }
        IList<Vector2> VertexUv { get; }
        IList<Vector2> VertexUv2 { get; }
        bool HasLightmap { get; }
        bool SkinPresent { get; }
    }

    public struct MdlFace
    {
        public int V1;
        public int V2;
        public int V3;
    }
}


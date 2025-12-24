using System;
using System.Numerics;

namespace Andastra.Parsing.Common
{
    // Matching PyKotor implementation at vendor/HoloLSP/vendor/pykotor/common/geometry.py:1094-1188
    // Original: class Face
    public class Face
    {
        public Vector3 V1 { get; set; }
        public Vector3 V2 { get; set; }
        public Vector3 V3 { get; set; }
        public SurfaceMaterial Material { get; set; }

        public Face(Vector3 v1, Vector3 v2, Vector3 v3, SurfaceMaterial material = SurfaceMaterial.Undefined)
        {
            V1 = v1;
            V2 = v2;
            V3 = v3;
            Material = material;
        }

        // Matching PyKotor implementation at vendor/HoloLSP/vendor/pykotor/common/geometry.py:1117-1135
        // Original: def normal(self) -> Vector3
        public Vector3 Normal()
        {
            Vector3 u = V2 - V1;
            Vector3 v = V3 - V2;

            Vector3 normal = new Vector3(
                (u.Y * v.Z) - (u.Z * v.Y),
                (u.Z * v.X) - (u.X * v.Z),
                (u.X * v.Y) - (u.Y * v.X)
            );
            normal = Vector3.Normalize(normal);

            return normal;
        }

        // Matching PyKotor implementation at vendor/HoloLSP/vendor/pykotor/common/geometry.py:1137-1143
        // Original: def area(self) -> float
        public float Area()
        {
            float a = Vector3.Distance(V1, V2);
            float b = Vector3.Distance(V1, V3);
            float c = Vector3.Distance(V2, V3);
            return 0.25f * (float)Math.Sqrt((a + b + c) * (-a + b + c) * (a - b + c) * (a + b - c));
        }

        // Matching PyKotor implementation at vendor/HoloLSP/vendor/pykotor/common/geometry.py:1145-1148
        // Original: def planar_distance(self) -> float
        public float PlanarDistance()
        {
            var normal = Normal();
            return -1.0f * Vector3.Dot(normal, V1);
        }

        // Matching PyKotor implementation at vendor/HoloLSP/vendor/pykotor/common/geometry.py:1150-1153
        // Original: def centre(self) -> Vector3
        public Vector3 Centre()
        {
            return (V1 + V2 + V3) / 3;
        }

        // Matching PyKotor implementation at vendor/HoloLSP/vendor/pykotor/common/geometry.py:1155-1164
        // Original: def average(self) -> Vector3
        public Vector3 Average()
        {
            return (V1 + V2 + V3) / 3;
        }

        // Matching PyKotor implementation at vendor/HoloLSP/vendor/pykotor/common/geometry.py:1166-1188
        // Original: def determine_z(self, x: float, y: float) -> float
        public float DetermineZ(float x, float y)
        {
            float dx1 = x - V1.X;
            float dy1 = y - V1.Y;
            float dx2 = V2.X - V1.X;
            float dy2 = V2.Y - V1.Y;
            float dx3 = V3.X - V1.X;
            float dy3 = V3.Z - V1.Y;  // Note: Python has this as v3.z - v1.y (potential bug, but matching 1:1)
            float scale = dx3 * dy2 - dx2 * dy3;
            float nx = (dx1 * dy2 - dy1 * dx2) / scale;
            float ny = (dy1 * dx3 - dx1 * dy3) / scale;
            return V1.Z + ny * (V2.Z - V1.Z) + nx * (V3.Z - V1.Z);
        }

        public override bool Equals(object obj)
        {
            if (obj is Face other)
            {
                return V1.Equals(other.V1) && V2.Equals(other.V2) && V3.Equals(other.V3) && Material == other.Material;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(V1, V2, V3, Material);
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using JetBrains.Annotations;

namespace BioWare.NET.Utility.Geometry
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/geometry.py:1454-1582
    // Original: class Polygon3:
    public class Polygon3 : IEnumerable<Vector3>
    {
        private readonly List<Vector3> _points = new List<Vector3>();

        public Polygon3(IEnumerable<Vector3> points = null)
        {
            if (points != null)
            {
                _points.AddRange(points);
            }
        }

        public int Count => _points.Count;

        public Vector3 this[int index]
        {
            get => _points[index];
            set => _points[index] = value;
        }

        public static Polygon3 FromPolygon2(Polygon2 poly2)
        {
            Polygon3 poly3 = new Polygon3();
            foreach (Vector2 point in poly2)
            {
                poly3._points.Add(new Vector3(point.X, point.Y, 0));
            }
            return poly3;
        }

        public void CreateTriangle(float size = 1.0f, Vector3 origin = default)
        {
            if (origin.Equals(default(Vector3)))
            {
                origin = Vector3.Zero;
            }
            float height = size * (float)(Math.Sqrt(3) / 2.0);
            _points.Clear();
            _points.Add(new Vector3(origin.X, origin.Y, origin.Z));
            _points.Add(new Vector3(origin.X + size, origin.Y, origin.Z));
            _points.Add(new Vector3(origin.X + size / 2, origin.Y + height, origin.Z));
        }

        public void DefaultSquare(float size = 1.0f, Vector3 origin = default)
        {
            if (origin.Equals(default(Vector3)))
            {
                origin = Vector3.Zero;
            }
            _points.Clear();
            _points.Add(new Vector3(origin.X, origin.Y, origin.Z));
            _points.Add(new Vector3(origin.X + size, origin.Y, origin.Z));
            _points.Add(new Vector3(origin.X + size, origin.Y + size, origin.Z));
            _points.Add(new Vector3(origin.X, origin.Y + size, origin.Z));
        }

        public void Append(Vector3 point)
        {
            _points.Add(point);
        }

        public void Extend(IEnumerable<Vector3> points)
        {
            _points.AddRange(points);
        }

        public void Remove(Vector3 point)
        {
            _points.Remove(point);
        }

        public int IndexOf(Vector3 point)
        {
            return _points.IndexOf(point);
        }

        public IEnumerator<Vector3> GetEnumerator()
        {
            return _points.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public override string ToString()
        {
            return $"Polygon3({string.Join(", ", _points)})";
        }
    }
}


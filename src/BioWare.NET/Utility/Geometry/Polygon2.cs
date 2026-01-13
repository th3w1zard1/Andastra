using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using JetBrains.Annotations;

namespace BioWare.NET.Utility.Geometry
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/geometry.py:1295-1452
    // Original: class Polygon2:
    public class Polygon2 : IEnumerable<Vector2>
    {
        private readonly List<Vector2> _points = new List<Vector2>();

        public Polygon2(IEnumerable<Vector2> points = null)
        {
            if (points != null)
            {
                _points.AddRange(points);
            }
        }

        public int Count => _points.Count;

        public Vector2 this[int index]
        {
            get => _points[index];
            set => _points[index] = value;
        }

        public static Polygon2 FromPolygon3(Polygon3 poly3)
        {
            Polygon2 poly2 = new Polygon2();
            foreach (Vector3 point in poly3)
            {
                poly2._points.Add(new Vector2(point.X, point.Y));
            }
            return poly2;
        }

        public bool Inside(Vector2 point, bool includeEdges = true)
        {
            int n = _points.Count;
            bool inside = false;

            Vector2 p1 = _points[0];
            for (int i = 1; i <= n; i++)
            {
                Vector2 p2 = _points[i % n];
                if (Math.Abs(p1.Y - p2.Y) < float.Epsilon)
                {
                    if (Math.Abs(point.Y - p2.Y) < float.Epsilon)
                    {
                        float minX = Math.Min(p1.X, p2.X);
                        float maxX = Math.Max(p1.X, p2.X);
                        if (minX <= point.X && point.X <= maxX)
                        {
                            inside = includeEdges;
                            break;
                        }
                        if (point.X < minX)
                        {
                            inside = !inside;
                        }
                    }
                }
                else
                {
                    float minY = Math.Min(p1.Y, p2.Y);
                    float maxY = Math.Max(p1.Y, p2.Y);
                    if (minY <= point.Y && point.Y <= maxY)
                    {
                        float xinters = (point.Y - p1.Y) * (p2.X - p1.X) / (p2.Y - p1.Y) + p1.X;

                        if (Math.Abs(point.X - xinters) < float.Epsilon)
                        {
                            inside = includeEdges;
                            break;
                        }

                        if (point.X < xinters)
                        {
                            inside = !inside;
                        }
                    }
                }
                p1 = p2;
            }
            return inside;
        }

        public float Area()
        {
            int n = _points.Count;
            float area = 0.0f;
            for (int i = 0; i < n - 1; i++)
            {
                area += -_points[i].Y * _points[i + 1].X + _points[i].X * _points[i + 1].Y;
            }
            area += -_points[n - 1].Y * _points[0].X + _points[n - 1].X * _points[0].Y;
            return 0.5f * Math.Abs(area);
        }

        public void Append(Vector2 point)
        {
            _points.Add(point);
        }

        public void Extend(IEnumerable<Vector2> points)
        {
            _points.AddRange(points);
        }

        public void Remove(Vector2 point)
        {
            _points.Remove(point);
        }

        public int IndexOf(Vector2 point)
        {
            return _points.IndexOf(point);
        }

        public IEnumerator<Vector2> GetEnumerator()
        {
            return _points.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public override string ToString()
        {
            return $"Polygon2({string.Join(", ", _points)})";
        }
    }
}


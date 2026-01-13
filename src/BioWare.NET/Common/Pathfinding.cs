using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using JetBrains.Annotations;

namespace BioWare.NET.Common
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/pathfinding.py:29-48
    // Original: @dataclass class PathfindingVertex:
    public class PathfindingVertex
    {
        public int Index { get; set; }
        public Vector3 Position { get; set; }
        public List<int> AdjacentIndices { get; set; }

        public PathfindingVertex(
            int index,
            Vector3 position,
            [CanBeNull] List<int> adjacentIndices = null)
        {
            Index = index;
            Position = position;
            AdjacentIndices = adjacentIndices ?? new List<int>();
        }
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/pathfinding.py:39-47
    // Original: @dataclass class PathfindingContextVertex:
    public class PathfindingContextVertex
    {
        public int Index { get; set; }
        public int ParentIndex { get; set; }
        public float Distance { get; set; }
        public float Heuristic { get; set; }
        public float TotalCost { get; set; }

        public PathfindingContextVertex(int index)
        {
            Index = index;
            ParentIndex = -1;
            Distance = 0.0f;
            Heuristic = 0.0f;
            TotalCost = 0.0f;
        }

        public PathfindingContextVertex(int index, int parentIndex, float distance, float heuristic, float totalCost)
        {
            Index = index;
            ParentIndex = parentIndex;
            Distance = distance;
            Heuristic = heuristic;
            TotalCost = totalCost;
        }
    }

    // Minimal interface mirroring the PTH usage in PyKotor (outgoing edges and point iteration).
    // Minimal contracts mirroring PTH usage in PyKotor pathfinding.
    public interface IPth
    {
        IEnumerable<PthPoint> Points();
        IEnumerable<PthEdge> Outgoing(int index);
    }

    public struct PthPoint
    {
        public float X;
        public float Y;
    }

    public struct PthEdge
    {
        public int Target;
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/pathfinding.py:49-318
    // Original: class Pathfinder:
    /// <summary>
    /// A* pathfinding implementation using PTH waypoint data.
    /// </summary>
    public class Pathfinder
    {
        private List<Vector3> _vertices = new List<Vector3>();
        private readonly Dictionary<int, List<int>> _adjacentVertices = new Dictionary<int, List<int>>();

        public Pathfinder()
        {
        }

        public void LoadFromPth(IPth pth, Dictionary<int, float> pointZ = null)
        {
            _vertices = new List<Vector3>();
            _adjacentVertices.Clear();
            Dictionary<int, float> zMap = pointZ ?? new Dictionary<int, float>();

            int i = 0;
            foreach (PthPoint point in pth.Points())
            {
                float z = zMap.ContainsKey(i) ? zMap[i] : 0.0f;
                _vertices.Add(new Vector3(point.X, point.Y, z));
                i++;
            }

            int count = _vertices.Count;
            for (int idx = 0; idx < count; idx++)
            {
                IEnumerable<PthEdge> outgoing = pth.Outgoing(idx) ?? Enumerable.Empty<PthEdge>();
                if (!_adjacentVertices.ContainsKey(idx))
                {
                    _adjacentVertices[idx] = new List<int>();
                }

                foreach (PthEdge edge in outgoing)
                {
                    int target = edge.Target;
                    if (!_adjacentVertices.ContainsKey(target))
                    {
                        _adjacentVertices[target] = new List<int>();
                    }

                    if (!_adjacentVertices[idx].Contains(target))
                    {
                        _adjacentVertices[idx].Add(target);
                    }
                    if (!_adjacentVertices[target].Contains(idx))
                    {
                        _adjacentVertices[target].Add(idx);
                    }
                }
            }
        }

        public void Load(IEnumerable<object> points, IEnumerable<Tuple<int, int>> connections = null, Dictionary<int, float> pointZ = null)
        {
            _vertices = new List<Vector3>();
            _adjacentVertices.Clear();
            Dictionary<int, float> zMap = pointZ ?? new Dictionary<int, float>();

            int i = 0;
            foreach (object point in points)
            {
                Vector3 vertex;
                Vector3? vec3 = point as Vector3?;
                if (vec3.HasValue)
                {
                    vertex = vec3.Value;
                }
                else
                {
                    Tuple<float, float> tuple = point as Tuple<float, float>;
                    if (tuple != null)
                    {
                        float z = zMap.ContainsKey(i) ? zMap[i] : 0.0f;
                        vertex = new Vector3(tuple.Item1, tuple.Item2, z);
                    }
                    else
                    {
                        float[] arr = point as float[];
                        if (arr != null && arr.Length >= 2)
                        {
                            float z = zMap.ContainsKey(i) ? zMap[i] : 0.0f;
                            vertex = new Vector3(arr[0], arr[1], z);
                        }
                        else
                        {
                            throw new ArgumentException("Point must be Vector3 or tuple/array of (x,y)");
                        }
                    }
                }

                _vertices.Add(vertex);
                i++;
            }

            if (connections != null)
            {
                foreach (Tuple<int, int> connection in connections)
                {
                    int source = connection.Item1;
                    int target = connection.Item2;

                    if (!_adjacentVertices.ContainsKey(source))
                    {
                        _adjacentVertices[source] = new List<int>();
                    }
                    if (!_adjacentVertices.ContainsKey(target))
                    {
                        _adjacentVertices[target] = new List<int>();
                    }

                    if (!_adjacentVertices[source].Contains(target))
                    {
                        _adjacentVertices[source].Add(target);
                    }
                    if (!_adjacentVertices[target].Contains(source))
                    {
                        _adjacentVertices[target].Add(source);
                    }
                }
            }
        }

        public List<Vector3> FindPath(Vector3 fromPos, Vector3 toPos)
        {
            if (_vertices.Count == 0)
            {
                return new List<Vector3> { fromPos, toPos };
            }

            int fromIdx = GetNearestVertex(fromPos);
            int toIdx = GetNearestVertex(toPos);

            if (fromIdx == toIdx)
            {
                return new List<Vector3> { fromPos, toPos };
            }

            Dictionary<int, PathfindingContextVertex> context = new Dictionary<int, PathfindingContextVertex>();
            HashSet<int> openSet = new HashSet<int> { fromIdx };
            HashSet<int> closedSet = new HashSet<int>();

            context[fromIdx] = new PathfindingContextVertex(fromIdx);

            while (openSet.Count > 0)
            {
                int currentIdx = GetVertexWithLeastCost(openSet, context);
                openSet.Remove(currentIdx);
                closedSet.Add(currentIdx);

                PathfindingContextVertex current = context[currentIdx];

                if (current.Index == toIdx)
                {
                    List<Vector3> path = new List<Vector3>();
                    int idx = current.Index;
                    while (idx != -1)
                    {
                        PathfindingContextVertex vert = context[idx];
                        path.Add(_vertices[vert.Index]);
                        idx = vert.ParentIndex;
                    }
                    path.Reverse();
                    return path;
                }

                List<int> adj = _adjacentVertices.ContainsKey(current.Index) ? _adjacentVertices[current.Index] : new List<int>();
                if (adj.Count == 0)
                {
                    continue;
                }

                foreach (int adjIdx in adj)
                {
                    if (closedSet.Contains(adjIdx))
                    {
                        continue;
                    }

                    float distance = current.Distance + DistanceSquared(_vertices[current.Index], _vertices[adjIdx]);
                    float heuristic = DistanceSquared(_vertices[adjIdx], _vertices[toIdx]);
                    float totalCost = distance + heuristic;

                    if (openSet.Contains(adjIdx))
                    {
                        PathfindingContextVertex existing = context[adjIdx];
                        if (distance > existing.Distance)
                        {
                            continue;
                        }
                        existing.ParentIndex = current.Index;
                        existing.Distance = distance;
                        existing.Heuristic = heuristic;
                        existing.TotalCost = totalCost;
                    }
                    else
                    {
                        PathfindingContextVertex child = new PathfindingContextVertex(adjIdx, current.Index, distance, heuristic, totalCost);
                        context[adjIdx] = child;
                        openSet.Add(adjIdx);
                    }
                }
            }

            return new List<Vector3> { fromPos, toPos };
        }

        private int GetNearestVertex(Vector3 point)
        {
            int nearestIdx = -1;
            float minDist = float.MaxValue;
            for (int i = 0; i < _vertices.Count; i++)
            {
                float dist = DistanceSquared(point, _vertices[i]);
                if (nearestIdx == -1 || dist < minDist)
                {
                    nearestIdx = i;
                    minDist = dist;
                }
            }
            return nearestIdx;
        }

        private int GetVertexWithLeastCost(HashSet<int> openSet, Dictionary<int, PathfindingContextVertex> context)
        {
            int bestIdx = -1;
            float bestCost = float.MaxValue;
            foreach (int idx in openSet)
            {
                PathfindingContextVertex vert = context[idx];
                if (bestIdx == -1 || vert.TotalCost < bestCost)
                {
                    bestIdx = idx;
                    bestCost = vert.TotalCost;
                }
            }
            return bestIdx;
        }

        private static float DistanceSquared(Vector3 a, Vector3 b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            float dz = a.Z - b.Z;
            return dx * dx + dy * dy + dz * dz;
        }
    }
}


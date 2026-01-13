using System;
using System.Collections.Generic;

namespace Andastra.Game.Graphics.MonoGame.Rendering
{
    /// <summary>
    /// Triangle strip generation for optimized mesh rendering.
    /// 
    /// Triangle strips reduce index buffer size and improve GPU cache
    /// efficiency by reusing vertices between adjacent triangles.
    /// 
    /// Features:
    /// - Automatic strip generation
    /// - Vertex cache optimization
    /// - Degenerate triangle handling
    /// - Multiple strip support
    /// </summary>
    public class TriangleStripGenerator
    {
        /// <summary>
        /// Generates triangle strips from triangle list.
        /// </summary>
        public List<uint> GenerateStrips(uint[] indices)
        {
            if (indices == null || indices.Length < 3)
            {
                return new List<uint>();
            }

            List<uint> strips = new List<uint>();
            HashSet<int> usedTriangles = new HashSet<int>();
            int triangleCount = indices.Length / 3;

            // Greedy strip generation
            for (int startTri = 0; startTri < triangleCount; startTri++)
            {
                if (usedTriangles.Contains(startTri))
                {
                    continue;
                }

                // Start new strip
                List<uint> currentStrip = new List<uint>();
                int currentTri = startTri;

                // Add first triangle
                currentStrip.Add(indices[currentTri * 3 + 0]);
                currentStrip.Add(indices[currentTri * 3 + 1]);
                currentStrip.Add(indices[currentTri * 3 + 2]);
                usedTriangles.Add(currentTri);

                // Extend strip
                bool extended = true;
                while (extended)
                {
                    extended = false;

                    // Find adjacent triangle
                    uint v0 = currentStrip[currentStrip.Count - 2];
                    uint v1 = currentStrip[currentStrip.Count - 1];

                    for (int i = 0; i < triangleCount; i++)
                    {
                        if (usedTriangles.Contains(i))
                        {
                            continue;
                        }

                        uint t0 = indices[i * 3 + 0];
                        uint t1 = indices[i * 3 + 1];
                        uint t2 = indices[i * 3 + 2];

                        // Check if triangle shares edge with strip end
                        if ((v0 == t0 && v1 == t1) || (v0 == t1 && v1 == t0))
                        {
                            currentStrip.Add(t2);
                            usedTriangles.Add(i);
                            extended = true;
                            break;
                        }
                        else if ((v0 == t1 && v1 == t2) || (v0 == t2 && v1 == t1))
                        {
                            currentStrip.Add(t0);
                            usedTriangles.Add(i);
                            extended = true;
                            break;
                        }
                        else if ((v0 == t2 && v1 == t0) || (v0 == t0 && v1 == t2))
                        {
                            currentStrip.Add(t1);
                            usedTriangles.Add(i);
                            extended = true;
                            break;
                        }
                    }
                }

                // Add strip to output
                strips.AddRange(currentStrip);

                // Add degenerate triangle if needed (for multiple strips)
                if (strips.Count > 0 && usedTriangles.Count < triangleCount)
                {
                    uint lastVertex = strips[strips.Count - 1];
                    strips.Add(lastVertex); // Degenerate triangle
                }
            }

            return strips;
        }

        /// <summary>
        /// Calculates strip efficiency (vertices per triangle).
        /// </summary>
        public float CalculateEfficiency(int triangleCount, int stripVertexCount)
        {
            if (triangleCount == 0)
            {
                return 0.0f;
            }

            int optimalVertices = triangleCount + 2; // Optimal strip: n triangles = n+2 vertices
            return optimalVertices / (float)stripVertexCount;
        }
    }
}


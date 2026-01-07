using System;

namespace Andastra.Runtime.MonoGame.Performance
{
    /// <summary>
    /// Render statistics for performance monitoring and debugging.
    /// </summary>
    public class RenderStatistics
    {
        /// <summary>
        /// Number of draw calls in the current frame.
        /// </summary>
        public int DrawCalls { get; set; }

        /// <summary>
        /// Number of triangles rendered in the current frame.
        /// </summary>
        public int TrianglesRendered { get; set; }

        /// <summary>
        /// Number of objects culled in the current frame.
        /// </summary>
        public int ObjectsCulled { get; set; }

        /// <summary>
        /// Frame time in milliseconds.
        /// </summary>
        public float FrameTimeMs { get; set; }

        /// <summary>
        /// Resets all statistics to zero.
        /// </summary>
        public void Reset()
        {
            DrawCalls = 0;
            TrianglesRendered = 0;
            ObjectsCulled = 0;
            FrameTimeMs = 0.0f;
        }
    }
}


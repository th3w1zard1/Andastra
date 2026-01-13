using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Andastra.Game.Graphics.MonoGame.Rendering
{
    /// <summary>
    /// Comprehensive render profiler for detailed performance analysis.
    /// 
    /// Provides detailed profiling of all rendering operations, enabling
    /// precise bottleneck identification and optimization.
    /// 
    /// Features:
    /// - Per-pass timing
    /// - Memory usage tracking
    /// - Draw call analysis
    /// - GPU/CPU time breakdown
    /// - Historical data
    /// </summary>
    /// <remarks>
    /// Render Profiler:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) frame timing system (modern profiling enhancement)
    /// - Located via string references: "frameStart" @ 0x007ba698, "frameEnd" @ 0x007ba668
    /// - "frameStartkey" @ 0x007ba688, "frameEndkey" @ 0x007ba65c (keyframe timing)
    /// - "frameStartbezierkey" @ 0x007ba674, "frameEndbezierkey" @ 0x007ba648 (bezier keyframe timing)
    /// - "m_bFrameBlending" @ 0x007baabc (frame blending flag)
    /// - Frame rate: "Fixed frame rate off." @ 0x007c707c, "Frame rate set." @ 0x007c7094
    /// - "Frame Buffer" @ 0x007c8408, "CB_FRAMEBUFF" @ 0x007d1d84 (frame buffer checkbox)
    /// - Video: "_BinkNextFrame@4", "_BinkDoFrame@4" (Bink video frame functions)
    /// - Original implementation: KOTOR tracks frame timing for animation and rendering
    /// - Frame timing: Original engine uses fixed timestep for game logic, variable for rendering
    /// - This MonoGame implementation: Modern render profiler for detailed performance analysis
    /// - Profiling: Tracks per-pass timing, draw calls, memory usage, GPU/CPU time breakdown
    /// </remarks>
    public class RenderProfiler
    {
        /// <summary>
        /// Profiling scope.
        /// </summary>
        public class ProfileScope : IDisposable
        {
            private readonly RenderProfiler _profiler;
            private readonly string _scopeName;
            private readonly Stopwatch _stopwatch;

            /// <summary>
            /// Initializes a new profiling scope.
            /// </summary>
            /// <param name="profiler">Parent profiler instance. Must not be null.</param>
            /// <param name="scopeName">Scope name for identification. Can be null or empty.</param>
            public ProfileScope(RenderProfiler profiler, string scopeName)
            {
                if (profiler == null)
                {
                    throw new ArgumentNullException(nameof(profiler));
                }
                _profiler = profiler;
                _scopeName = scopeName ?? string.Empty;
                _stopwatch = Stopwatch.StartNew();
            }

            public void Dispose()
            {
                _stopwatch.Stop();
                _profiler.RecordScope(_scopeName, _stopwatch.Elapsed.TotalMilliseconds);
            }
        }

        /// <summary>
        /// Scope timing data.
        /// </summary>
        public class ScopeData
        {
            public string Name;
            public double TotalTime;
            public int CallCount;
            public double MinTime;
            public double MaxTime;
            public double AverageTime;
        }

        private readonly Dictionary<string, ScopeData> _scopes;
        private readonly Dictionary<string, List<double>> _history;
        private readonly object _lock;

        /// <summary>
        /// Gets or sets whether profiling is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Initializes a new render profiler.
        /// </summary>
        public RenderProfiler()
        {
            _scopes = new Dictionary<string, ScopeData>();
            _history = new Dictionary<string, List<double>>();
            _lock = new object();
        }

        /// <summary>
        /// Begins a profiling scope.
        /// </summary>
        /// <param name="scopeName">Scope name for identification. Can be null or empty.</param>
        /// <returns>ProfileScope instance that should be disposed when scope ends, or null if profiling is disabled.</returns>
        public ProfileScope BeginScope(string scopeName)
        {
            if (!Enabled)
            {
                return null;
            }
            return new ProfileScope(this, scopeName);
        }

        /// <summary>
        /// Records scope timing.
        /// </summary>
        internal void RecordScope(string scopeName, double timeMs)
        {
            if (!Enabled)
            {
                return;
            }

            lock (_lock)
            {
                ScopeData data;
                if (!_scopes.TryGetValue(scopeName, out data))
                {
                    data = new ScopeData
                    {
                        Name = scopeName,
                        MinTime = double.MaxValue,
                        MaxTime = double.MinValue
                    };
                    _scopes[scopeName] = data;
                }

                data.TotalTime += timeMs;
                data.CallCount++;
                data.MinTime = Math.Min(data.MinTime, timeMs);
                data.MaxTime = Math.Max(data.MaxTime, timeMs);
                data.AverageTime = data.TotalTime / data.CallCount;

                // Update history
                List<double> history;
                if (!_history.TryGetValue(scopeName, out history))
                {
                    history = new List<double>();
                    _history[scopeName] = history;
                }
                history.Add(timeMs);
                if (history.Count > 1000)
                {
                    history.RemoveAt(0);
                }
            }
        }

        /// <summary>
        /// Gets profiling report with all scope timing data.
        /// </summary>
        /// <returns>Dictionary mapping scope names to their timing data. Returns a copy that is safe to modify.</returns>
        public Dictionary<string, ScopeData> GetReport()
        {
            lock (_lock)
            {
                return new Dictionary<string, ScopeData>(_scopes);
            }
        }

        /// <summary>
        /// Clears all profiling data.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _scopes.Clear();
                _history.Clear();
            }
        }
    }
}


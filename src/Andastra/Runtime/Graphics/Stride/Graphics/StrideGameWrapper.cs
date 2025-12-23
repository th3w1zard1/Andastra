using System;
using global::Stride.Engine;
using Stride.Graphics;

namespace Andastra.Runtime.Stride.Graphics
{
    /// <summary>
    /// Wrapper for Stride.Engine.Game that provides UpdateFrame and DrawFrame events.
    /// Stride's Game class uses Update() and Draw() methods, but we need event-based callbacks.
    /// </summary>
    public class StrideGameWrapper : Game
    {
        /// <summary>
        /// Event raised before each update frame.
        /// </summary>
        public event EventHandler<FrameEventArgs> UpdateFrame;

        /// <summary>
        /// Event raised before each draw frame.
        /// </summary>
        public event EventHandler<FrameEventArgs> DrawFrame;

        /// <summary>
        /// Frame event arguments containing elapsed time.
        /// </summary>
        public class FrameEventArgs : EventArgs
        {
            public TimeSpan Elapsed { get; set; }
        }

        protected override void Update(TimeSpan elapsed)
        {
            base.Update(elapsed);
            UpdateFrame?.Invoke(this, new FrameEventArgs { Elapsed = elapsed });
        }

        protected override void Draw(TimeSpan elapsed)
        {
            base.Draw(elapsed);
            DrawFrame?.Invoke(this, new FrameEventArgs { Elapsed = elapsed });
        }
    }
}


using System;
using global::Stride.Engine;
using global::Stride.Games;
using Stride.Graphics;

namespace Andastra.Game.Stride.Graphics
{
    /// <summary>
    /// Wrapper for Stride.Engine.Game that provides UpdateFrame and DrawFrame events.
    /// Stride's Game class uses Update() and Draw() methods, but we need event-based callbacks.
    /// </summary>
    public class StrideGameWrapper : global::Stride.Engine.Game
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
        /// Event raised when the game is initialized.
        /// </summary>
        public event EventHandler Initialized;

        /// <summary>
        /// Frame event arguments containing elapsed time.
        /// </summary>
        public class FrameEventArgs : EventArgs
        {
            public TimeSpan Elapsed { get; set; }
        }

        protected override void Initialize()
        {
            base.Initialize();
            Initialized?.Invoke(this, EventArgs.Empty);
        }

        protected override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            UpdateFrame?.Invoke(this, new FrameEventArgs { Elapsed = gameTime.Elapsed });
        }

        protected override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);
            DrawFrame?.Invoke(this, new FrameEventArgs { Elapsed = gameTime.Elapsed });
        }
    }
}


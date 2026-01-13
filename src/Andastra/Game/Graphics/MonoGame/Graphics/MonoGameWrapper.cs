using System;
using Microsoft.Xna.Framework;

namespace Andastra.Game.Graphics.MonoGame.Graphics
{
    /// <summary>
    /// Wrapper for Microsoft.Xna.Framework.Game that provides UpdateFrame and DrawFrame events.
    /// MonoGame's Game class uses protected virtual Update() and Draw() methods,
    /// so we need this wrapper to expose event-based callbacks for external use.
    /// </summary>
    /// <remarks>
    /// Based on MonoGame API: Game class lifecycle methods are protected virtual
    /// - Initialize(): Called once after Game.Run() is called
    /// - LoadContent(): Called once after Initialize() completes
    /// - Update(GameTime): Called every frame for game logic
    /// - Draw(GameTime): Called every frame for rendering
    /// - UnloadContent(): Called when the game is exiting
    /// 
    /// This wrapper exposes Update and Draw as events so external code can hook in.
    /// </remarks>
    public class MonoGameWrapper : Microsoft.Xna.Framework.Game
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
            public TimeSpan TotalTime { get; set; }
        }

        protected override void Initialize()
        {
            base.Initialize();
            Initialized?.Invoke(this, EventArgs.Empty);
        }

        protected override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            UpdateFrame?.Invoke(this, new FrameEventArgs 
            { 
                Elapsed = gameTime.ElapsedGameTime,
                TotalTime = gameTime.TotalGameTime
            });
        }

        protected override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);
            DrawFrame?.Invoke(this, new FrameEventArgs 
            { 
                Elapsed = gameTime.ElapsedGameTime,
                TotalTime = gameTime.TotalGameTime
            });
        }
    }
}


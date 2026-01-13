using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Game.Graphics.MonoGame.UI
{
    /// <summary>
    /// Screen fade system for transitions (fade in/out effects).
    /// </summary>
    /// <remarks>
    /// Screen Fade System:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) screen fade system
    /// - Located via string references: "FadeDelayOnDeath" @ 0x007bf55c, "FadeLength" @ 0x007c3580, "FadeDelay" @ 0x007c358c
    /// - "FadeColor" @ 0x007c3598, "FadeType" @ 0x007c35a4, "FadeTime" @ 0x007c60ec
    /// - "fade_p" @ 0x007c8790 (fade panel GUI), "donefade" @ 0x007cdb94 (fade completion)
    /// - Original implementation: Fades screen to black for module transitions, loading screens, death sequences
    /// - Fade duration: Typically 0.5-1.0 seconds for smooth transitions (FadeLength parameter)
    /// - Fade delay: Delay before fade starts (FadeDelay parameter)
    /// - Fade color: Can be black, white, or custom color (FadeColor parameter)
    /// - Fade type: Fade in (from black) or fade out (to black) (FadeType parameter)
    /// - Used for: Module transitions, area transitions, loading screens, cutscenes, death sequences
    /// </remarks>
    public class ScreenFade
    {
        private readonly SpriteBatch _spriteBatch;
        private Texture2D _fadeTexture;
        private float _fadeAlpha;
        private float _fadeSpeed;
        private bool _isFading;
        private bool _fadeDirection; // true = fade out (to black), false = fade in (from black)
        private float _targetAlpha;

        /// <summary>
        /// Gets whether a fade is currently in progress.
        /// </summary>
        public bool IsFading
        {
            get { return _isFading; }
        }

        /// <summary>
        /// Gets the current fade alpha (0.0 = transparent, 1.0 = opaque black).
        /// </summary>
        public float FadeAlpha
        {
            get { return _fadeAlpha; }
        }

        /// <summary>
        /// Gets whether the screen is fully faded out (black).
        /// </summary>
        public bool IsFadedOut
        {
            get { return _fadeAlpha >= 1.0f && !_fadeDirection; }
        }

        /// <summary>
        /// Gets whether the screen is fully faded in (transparent).
        /// </summary>
        public bool IsFadedIn
        {
            get { return _fadeAlpha <= 0.0f && _fadeDirection; }
        }

        /// <summary>
        /// Event fired when fade out completes.
        /// </summary>
        public event Action OnFadeOutComplete;

        /// <summary>
        /// Event fired when fade in completes.
        /// </summary>
        public event Action OnFadeInComplete;

        public ScreenFade(GraphicsDevice device)
        {
            if (device == null)
            {
                throw new ArgumentNullException("device");
            }

            _spriteBatch = new SpriteBatch(device);
            _fadeTexture = new Texture2D(device, 1, 1);
            _fadeTexture.SetData(new[] { Color.Black });
            _fadeAlpha = 0.0f;
            _fadeSpeed = 2.0f; // Default: 2.0 alpha units per second (0.5 second fade)
            _isFading = false;
            _fadeDirection = false;
            _targetAlpha = 0.0f;
        }

        /// <summary>
        /// Starts a fade out (to black).
        /// </summary>
        /// <param name="duration">Fade duration in seconds (default: 0.5).</param>
        public void FadeOut(float duration = 0.5f)
        {
            if (duration <= 0f)
            {
                duration = 0.5f;
            }

            _fadeSpeed = 1.0f / duration; // Calculate speed to complete fade in specified duration
            _targetAlpha = 1.0f;
            _fadeDirection = true; // Fading out
            _isFading = true;
        }

        /// <summary>
        /// Starts a fade in (from black).
        /// </summary>
        /// <param name="duration">Fade duration in seconds (default: 0.5).</param>
        public void FadeIn(float duration = 0.5f)
        {
            if (duration <= 0f)
            {
                duration = 0.5f;
            }

            _fadeSpeed = 1.0f / duration; // Calculate speed to complete fade in specified duration
            _targetAlpha = 0.0f;
            _fadeDirection = false; // Fading in
            _isFading = true;
        }

        /// <summary>
        /// Instantly sets fade to fully black (no transition).
        /// </summary>
        public void SetFadedOut()
        {
            _fadeAlpha = 1.0f;
            _isFading = false;
            _fadeDirection = false;
        }

        /// <summary>
        /// Instantly sets fade to fully transparent (no transition).
        /// </summary>
        public void SetFadedIn()
        {
            _fadeAlpha = 0.0f;
            _isFading = false;
            _fadeDirection = true;
        }

        /// <summary>
        /// Updates the fade system.
        /// </summary>
        public void Update(float deltaTime)
        {
            if (!_isFading)
            {
                return;
            }

            // Update fade alpha
            if (_fadeDirection)
            {
                // Fading out (to black)
                _fadeAlpha += _fadeSpeed * deltaTime;
                if (_fadeAlpha >= _targetAlpha)
                {
                    _fadeAlpha = _targetAlpha;
                    _isFading = false;
                    OnFadeOutComplete?.Invoke();
                }
            }
            else
            {
                // Fading in (from black)
                _fadeAlpha -= _fadeSpeed * deltaTime;
                if (_fadeAlpha <= _targetAlpha)
                {
                    _fadeAlpha = _targetAlpha;
                    _isFading = false;
                    OnFadeInComplete?.Invoke();
                }
            }

            // Clamp alpha
            _fadeAlpha = Math.Max(0.0f, Math.Min(1.0f, _fadeAlpha));
        }

        /// <summary>
        /// Draws the fade overlay.
        /// </summary>
        public void Draw(GameTime gameTime)
        {
            if (_fadeAlpha <= 0.0f)
            {
                return; // No fade, skip drawing
            }

            int viewportWidth = _spriteBatch.GraphicsDevice.Viewport.Width;
            int viewportHeight = _spriteBatch.GraphicsDevice.Viewport.Height;

            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            _spriteBatch.Draw(_fadeTexture, new Rectangle(0, 0, viewportWidth, viewportHeight),
                Color.White * _fadeAlpha);
            _spriteBatch.End();
        }

        /// <summary>
        /// Disposes resources.
        /// </summary>
        public void Dispose()
        {
            if (_fadeTexture != null)
            {
                _fadeTexture.Dispose();
                _fadeTexture = null;
            }
            if (_spriteBatch != null)
            {
                _spriteBatch.Dispose();
            }
        }
    }
}


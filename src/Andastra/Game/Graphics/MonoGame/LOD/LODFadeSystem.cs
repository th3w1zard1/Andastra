using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Andastra.Game.Graphics.MonoGame.LOD
{
    /// <summary>
    /// LOD fade system for smooth transitions between LOD levels.
    /// 
    /// Provides smooth alpha-blended transitions between LOD levels to
    /// eliminate visual popping when switching LODs.
    /// 
    /// Features:
    /// - Smooth alpha blending between LODs
    /// - Temporal coherence
    /// - Configurable fade distance
    /// - Per-object fade tracking
    /// </summary>
    /// <remarks>
    /// LOD Fade System:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) fade system
    /// - Located via string references: "FadeDelayOnDeath" @ 0x007bf55c (fade delay on death)
    /// - "FadeLength" @ 0x007c3580, "FadeDelay" @ 0x007c358c, "FadeColor" @ 0x007c3598
    /// - "FadeType" @ 0x007c35a4, "FadeTime" @ 0x007c60ec
    /// - GUI: "fade_p" @ 0x007c8790 (fade panel), "donefade" @ 0x007cdb94 (fade complete flag)
    /// - Original implementation: KOTOR uses fade effects for transitions, death, and screen effects
    /// - Fade types: Screen fade (full screen), object fade (per-object alpha), LOD fade (smooth LOD transitions)
    /// - Fade timing: FadeDelay controls when fade starts, FadeLength controls duration
    /// - FadeColor: RGBA color for screen fade effects
    /// </remarks>
    public class LODFadeSystem
    {
        /// <summary>
        /// LOD fade state for an object.
        /// </summary>
        private class LODFadeState
        {
            public int ObjectId;
            public int CurrentLOD;
            public int TargetLOD;
            public float FadeAlpha;
            public float FadeDirection; // 1.0 = fading in, -1.0 = fading out
        }

        private readonly Dictionary<int, LODFadeState> _fadeStates;
        private float _fadeDistance;
        private float _fadeSpeed;

        /// <summary>
        /// Gets or sets the fade distance (distance over which to fade).
        /// </summary>
        public float FadeDistance
        {
            get { return _fadeDistance; }
            set { _fadeDistance = Math.Max(0.0f, value); }
        }

        /// <summary>
        /// Gets or sets the fade speed (units per second).
        /// </summary>
        public float FadeSpeed
        {
            get { return _fadeSpeed; }
            set { _fadeSpeed = Math.Max(0.0f, value); }
        }

        /// <summary>
        /// Initializes a new LOD fade system.
        /// </summary>
        public LODFadeSystem()
        {
            _fadeStates = new Dictionary<int, LODFadeState>();
            _fadeDistance = 5.0f;
            _fadeSpeed = 10.0f;
        }

        /// <summary>
        /// Updates fade states for all objects.
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last update in seconds. Will be clamped to non-negative if negative.</param>
        public void Update(float deltaTime)
        {
            if (deltaTime < 0.0f)
            {
                deltaTime = 0.0f; // Clamp negative deltas
            }

            var keysToRemove = new List<int>();

            foreach (var kvp in _fadeStates)
            {
                LODFadeState state = kvp.Value;

                // Update fade alpha
                state.FadeAlpha += state.FadeDirection * (_fadeSpeed * deltaTime);
                state.FadeAlpha = Math.Max(0.0f, Math.Min(1.0f, state.FadeAlpha));

                // Check if fade is complete
                if (state.FadeAlpha <= 0.0f && state.FadeDirection < 0.0f)
                {
                    // Fade out complete, switch to target LOD
                    state.CurrentLOD = state.TargetLOD;
                    state.FadeAlpha = 0.0f;
                    keysToRemove.Add(kvp.Key);
                }
                else if (state.FadeAlpha >= 1.0f && state.FadeDirection > 0.0f)
                {
                    // Fade in complete
                    state.FadeAlpha = 1.0f;
                    keysToRemove.Add(kvp.Key);
                }
            }

            // Remove completed fades
            foreach (int key in keysToRemove)
            {
                _fadeStates.Remove(key);
            }
        }

        /// <summary>
        /// Requests a LOD transition for an object.
        /// </summary>
        /// <param name="objectId">Unique identifier for the object.</param>
        /// <param name="currentLOD">Current LOD level (typically 0-3, where 0 is highest detail).</param>
        /// <param name="targetLOD">Target LOD level to transition to.</param>
        /// <remarks>
        /// If currentLOD equals targetLOD, the fade state for this object is removed (no transition needed).
        /// Otherwise, a fade transition is initiated that will smoothly blend between the two LOD levels.
        /// </remarks>
        public void RequestLODTransition(int objectId, int currentLOD, int targetLOD)
        {
            if (currentLOD == targetLOD)
            {
                // No transition needed
                _fadeStates.Remove(objectId);
                return;
            }

            LODFadeState state;
            if (!_fadeStates.TryGetValue(objectId, out state))
            {
                state = new LODFadeState
                {
                    ObjectId = objectId,
                    CurrentLOD = currentLOD,
                    TargetLOD = targetLOD,
                    FadeAlpha = 1.0f,
                    FadeDirection = -1.0f // Start fading out
                };
                _fadeStates[objectId] = state;
            }
            else
            {
                // Update existing fade
                state.TargetLOD = targetLOD;
            }
        }

        /// <summary>
        /// Gets the fade alpha for an object.
        /// </summary>
        /// <param name="objectId">Unique identifier for the object.</param>
        /// <returns>Fade alpha value (0.0 = fully transparent, 1.0 = fully opaque). Returns 1.0 if object is not fading.</returns>
        public float GetFadeAlpha(int objectId)
        {
            LODFadeState state;
            if (_fadeStates.TryGetValue(objectId, out state))
            {
                return state.FadeAlpha;
            }
            return 1.0f; // Fully opaque if not fading
        }

        /// <summary>
        /// Gets the current LOD for an object (may be transitioning).
        /// </summary>
        /// <param name="objectId">Unique identifier for the object.</param>
        /// <returns>Current LOD level, or -1 if object is not tracked.</returns>
        public int GetCurrentLOD(int objectId)
        {
            LODFadeState state;
            if (_fadeStates.TryGetValue(objectId, out state))
            {
                return state.CurrentLOD;
            }
            return -1;
        }

        /// <summary>
        /// Gets the target LOD for an object (may be transitioning).
        /// </summary>
        /// <param name="objectId">Unique identifier for the object.</param>
        /// <returns>Target LOD level, or -1 if object is not tracked.</returns>
        public int GetTargetLOD(int objectId)
        {
            LODFadeState state;
            if (_fadeStates.TryGetValue(objectId, out state))
            {
                return state.TargetLOD;
            }
            return -1;
        }
    }
}


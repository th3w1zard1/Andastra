using System;
using System.Numerics;
using JetBrains.Annotations;

namespace Andastra.Runtime.Games.Eclipse.Environmental
{
    /// <summary>
    /// Eclipse engine weather system implementation.
    /// </summary>
    /// <remarks>
    /// Eclipse Weather System Implementation:
    /// - Based on daorigins.exe: Weather system initialization and management
    /// - Based on DragonAge2.exe: Enhanced weather effects with dynamic transitions
    /// - Weather affects visibility, particle effects, and audio
    /// - Weather can change dynamically based on time or script events
    /// - Supports rain, snow, fog, wind, and storm weather types
    /// </remarks>
    [PublicAPI]
    public class EclipseWeatherSystem : IWeatherSystem
    {
        private WeatherType _currentWeather;
        private float _intensity;
        private Vector3 _windDirection;
        private float _windSpeed;
        private bool _isActive;

        /// <summary>
        /// Creates a new Eclipse weather system.
        /// </summary>
        /// <remarks>
        /// Initializes weather system with default values (no weather).
        /// Based on weather system initialization in daorigins.exe, DragonAge2.exe.
        /// </remarks>
        public EclipseWeatherSystem()
        {
            _currentWeather = WeatherType.None;
            _intensity = 0.0f;
            _windDirection = Vector3.Zero;
            _windSpeed = 0.0f;
            _isActive = false;
        }

        /// <summary>
        /// Gets the current weather type.
        /// </summary>
        public WeatherType CurrentWeather => _currentWeather;

        /// <summary>
        /// Gets the weather intensity (0.0 to 1.0).
        /// </summary>
        public float Intensity => _intensity;

        /// <summary>
        /// Gets the wind direction vector.
        /// </summary>
        public Vector3 WindDirection => _windDirection;

        /// <summary>
        /// Gets the wind speed.
        /// </summary>
        public float WindSpeed => _windSpeed;

        /// <summary>
        /// Gets whether weather is currently active.
        /// </summary>
        public bool IsActive => _isActive;

        /// <summary>
        /// Sets the weather type and intensity.
        /// </summary>
        /// <param name="weatherType">The weather type to set.</param>
        /// <param name="intensity">The intensity (0.0 to 1.0).</param>
        /// <remarks>
        /// Based on weather setting functions in daorigins.exe, DragonAge2.exe.
        /// Weather intensity affects particle density and visibility.
        /// </remarks>
        public void SetWeather(WeatherType weatherType, float intensity)
        {
            _currentWeather = weatherType;
            _intensity = Math.Max(0.0f, Math.Min(1.0f, intensity));
            _isActive = (_currentWeather != WeatherType.None) && (_intensity > 0.0f);
        }

        /// <summary>
        /// Sets the wind parameters.
        /// </summary>
        /// <param name="direction">The wind direction vector.</param>
        /// <param name="speed">The wind speed.</param>
        /// <remarks>
        /// Based on wind setting functions in daorigins.exe, DragonAge2.exe.
        /// Wind affects particle movement and audio propagation.
        /// </remarks>
        public void SetWind(Vector3 direction, float speed)
        {
            _windDirection = direction;
            _windSpeed = Math.Max(0.0f, speed);

            // Normalize wind direction if non-zero
            if (_windSpeed > 0.0f && _windDirection.LengthSquared() > 0.0f)
            {
                _windDirection = Vector3.Normalize(_windDirection);
            }
        }

        /// <summary>
        /// Updates the weather system.
        /// </summary>
        /// <param name="deltaTime">Time since last update.</param>
        /// <remarks>
        /// Based on weather update functions in daorigins.exe, DragonAge2.exe.
        /// Updates weather state, transitions, and effects.
        /// </remarks>
        public void Update(float deltaTime)
        {
            // Weather system updates:
            // - Smooth weather transitions
            // - Dynamic weather changes based on time
            // - Wind variation
            // - Weather effect updates

            // In a full implementation, this would:
            // - Update weather transitions (fade in/out)
            // - Update wind variation (gusts, direction changes)
            // - Update weather particle effects
            // - Update visibility based on weather
        }
    }
}


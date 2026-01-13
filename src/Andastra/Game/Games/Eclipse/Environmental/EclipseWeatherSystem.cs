using System;
using System.Numerics;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Eclipse.Environmental
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
    /// - Weather transitions: Smooth fade in/out between weather states
    /// - daorigins.exe: Weather transitions handled by weather update function
    /// - DragonAge2.exe: Enhanced transition system with duration and interpolation
    /// </remarks>
    [PublicAPI]
    public class EclipseWeatherSystem : IWeatherSystem
    {
        private WeatherType _currentWeather;
        private float _intensity;
        private Vector3 _windDirection;
        private float _windSpeed;
        private bool _isActive;

        // Weather transition state
        // Based on daorigins.exe: Weather transitions use interpolation between states
        // DragonAge2.exe: Transition duration and smooth interpolation for weather changes
        private bool _isTransitioning;
        private WeatherType _targetWeather;
        private float _targetIntensity;
        private float _transitionDuration;
        private float _transitionElapsed;
        private float _startIntensity;
        private Vector3 _startWindDirection;
        private float _startWindSpeed;
        private Vector3 _targetWindDirection;
        private float _targetWindSpeed;

        // Wind variation state
        // Based on daorigins.exe: Wind can vary over time with gusts and direction changes
        // DragonAge2.exe: Enhanced wind variation with smooth direction changes
        private float _windVariationTimer;
        private float _windVariationInterval;
        private Vector3 _baseWindDirection;
        private float _baseWindSpeed;

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

            // Initialize transition state
            _isTransitioning = false;
            _targetWeather = WeatherType.None;
            _targetIntensity = 0.0f;
            _transitionDuration = 0.0f;
            _transitionElapsed = 0.0f;
            _startIntensity = 0.0f;
            _startWindDirection = Vector3.Zero;
            _startWindSpeed = 0.0f;
            _targetWindDirection = Vector3.Zero;
            _targetWindSpeed = 0.0f;

            // Initialize wind variation state
            _windVariationTimer = 0.0f;
            _windVariationInterval = 10.0f; // Default: wind variation every 10 seconds
            _baseWindDirection = Vector3.Zero;
            _baseWindSpeed = 0.0f;
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
        /// If a transition is in progress, this will cancel it and set weather immediately.
        /// </remarks>
        public void SetWeather(WeatherType weatherType, float intensity)
        {
            _currentWeather = weatherType;
            _intensity = Math.Max(0.0f, Math.Min(1.0f, intensity));
            _isActive = (_currentWeather != WeatherType.None) && (_intensity > 0.0f);

            // Cancel any active transition
            _isTransitioning = false;
        }

        /// <summary>
        /// Starts a smooth transition to a new weather state.
        /// </summary>
        /// <param name="targetWeather">The target weather type.</param>
        /// <param name="targetIntensity">The target intensity (0.0 to 1.0).</param>
        /// <param name="duration">The transition duration in seconds.</param>
        /// <param name="targetWindDirection">Optional target wind direction (null to keep current).</param>
        /// <param name="targetWindSpeed">Optional target wind speed (null to keep current).</param>
        /// <remarks>
        /// Based on weather transition functions in daorigins.exe, DragonAge2.exe.
        /// Weather transitions smoothly interpolate between current and target states.
        /// Transition duration determines how long the fade takes (default: 5.0 seconds).
        /// If duration is 0 or negative, weather changes immediately without transition.
        /// </remarks>
        public void TransitionToWeather(
            WeatherType targetWeather,
            float targetIntensity,
            float duration = 5.0f,
            [CanBeNull] Vector3? targetWindDirection = null,
            [CanBeNull] float? targetWindSpeed = null)
        {
            // Clamp target intensity
            targetIntensity = Math.Max(0.0f, Math.Min(1.0f, targetIntensity));

            // If duration is 0 or negative, change immediately
            if (duration <= 0.0f)
            {
                SetWeather(targetWeather, targetIntensity);
                if (targetWindDirection.HasValue)
                {
                    SetWind(targetWindDirection.Value, targetWindSpeed ?? _windSpeed);
                }
                return;
            }

            // If already transitioning to the same state, update duration only
            if (_isTransitioning && _targetWeather == targetWeather && Math.Abs(_targetIntensity - targetIntensity) < 0.01f)
            {
                _transitionDuration = duration;
                _transitionElapsed = 0.0f;
                return;
            }

            // Start new transition
            _isTransitioning = true;
            _targetWeather = targetWeather;
            _targetIntensity = targetIntensity;
            _transitionDuration = duration;
            _transitionElapsed = 0.0f;

            // Store starting state for interpolation
            _startIntensity = _intensity;
            _startWindDirection = _windDirection;
            _startWindSpeed = _windSpeed;

            // Set target wind parameters
            if (targetWindDirection.HasValue)
            {
                _targetWindDirection = targetWindDirection.Value;
                if (_targetWindDirection.LengthSquared() > 0.0f)
                {
                    _targetWindDirection = Vector3.Normalize(_targetWindDirection);
                }
            }
            else
            {
                _targetWindDirection = _windDirection;
            }

            _targetWindSpeed = targetWindSpeed ?? _windSpeed;
        }

        /// <summary>
        /// Sets the wind parameters.
        /// </summary>
        /// <param name="direction">The wind direction vector.</param>
        /// <param name="speed">The wind speed.</param>
        /// <remarks>
        /// Based on wind setting functions in daorigins.exe, DragonAge2.exe.
        /// Wind affects particle movement and audio propagation.
        /// Sets base wind parameters for variation system.
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

            // Update base wind parameters for variation
            _baseWindDirection = _windDirection;
            _baseWindSpeed = _windSpeed;
        }

        /// <summary>
        /// Gets whether a weather transition is currently in progress.
        /// </summary>
        public bool IsTransitioning => _isTransitioning;

        /// <summary>
        /// Gets the target weather type for the current transition.
        /// </summary>
        public WeatherType TargetWeather => _targetWeather;

        /// <summary>
        /// Gets the transition progress (0.0 to 1.0).
        /// </summary>
        public float TransitionProgress
        {
            get
            {
                if (!_isTransitioning || _transitionDuration <= 0.0f)
                {
                    return 1.0f;
                }
                return Math.Min(1.0f, _transitionElapsed / _transitionDuration);
            }
        }

        /// <summary>
        /// Updates the weather system.
        /// </summary>
        /// <param name="deltaTime">Time since last update.</param>
        /// <remarks>
        /// Based on weather update functions in daorigins.exe, DragonAge2.exe.
        /// Updates weather state, transitions, and effects.
        /// daorigins.exe: Weather update function handles transitions and wind variation
        /// DragonAge2.exe: Enhanced update with smooth interpolation and particle effects
        /// </remarks>
        public void Update(float deltaTime)
        {
            // Update weather transition
            // Based on daorigins.exe: Weather transitions use linear interpolation
            // DragonAge2.exe: Smooth interpolation with easing for natural transitions
            if (_isTransitioning)
            {
                _transitionElapsed += deltaTime;

                if (_transitionElapsed >= _transitionDuration)
                {
                    // Transition complete
                    _currentWeather = _targetWeather;
                    _intensity = _targetIntensity;
                    _windDirection = _targetWindDirection;
                    _windSpeed = _targetWindSpeed;
                    _isActive = (_currentWeather != WeatherType.None) && (_intensity > 0.0f);
                    _isTransitioning = false;
                }
                else
                {
                    // Interpolate between start and target states
                    // Use smooth interpolation (ease-in-out) for natural transitions
                    float t = _transitionElapsed / _transitionDuration;
                    float smoothT = SmoothStep(0.0f, 1.0f, t);

                    // Interpolate intensity
                    _intensity = Lerp(_startIntensity, _targetIntensity, smoothT);

                    // Interpolate wind direction
                    _windDirection = Vector3.Lerp(_startWindDirection, _targetWindDirection, smoothT);
                    if (_windDirection.LengthSquared() > 0.0001f)
                    {
                        _windDirection = Vector3.Normalize(_windDirection);
                    }

                    // Interpolate wind speed
                    _windSpeed = Lerp(_startWindSpeed, _targetWindSpeed, smoothT);

                    // Update active state based on current weather
                    // During transition, weather is active if either start or target is active
                    bool startActive = (_currentWeather != WeatherType.None) && (_startIntensity > 0.0f);
                    bool targetActive = (_targetWeather != WeatherType.None) && (_targetIntensity > 0.0f);
                    _isActive = startActive || targetActive;

                    // If transitioning between different weather types, update current weather
                    // when intensity crosses midpoint (for smooth type transitions)
                    if (_currentWeather != _targetWeather)
                    {
                        if (t >= 0.5f)
                        {
                            _currentWeather = _targetWeather;
                        }
                    }
                }
            }

            // Update wind variation
            // Based on daorigins.exe: Wind can vary over time with gusts and direction changes
            // DragonAge2.exe: Enhanced wind variation with smooth direction changes
            if (!_isTransitioning && _baseWindSpeed > 0.0f)
            {
                _windVariationTimer += deltaTime;

                if (_windVariationTimer >= _windVariationInterval)
                {
                    // Apply wind variation
                    // Based on daorigins.exe: Wind variation uses random direction and speed changes
                    // Variation is subtle to maintain base wind characteristics
                    Random random = new Random();
                    float variationAngle = (float)(random.NextDouble() * 0.5f - 0.25f); // ±15 degrees
                    float variationSpeed = (float)(random.NextDouble() * 0.2f - 0.1f); // ±10% speed

                    // Rotate wind direction slightly
                    if (Math.Abs(variationAngle) > 0.01f)
                    {
                        // Create rotation quaternion for wind direction variation
                        // Simplified: rotate around up vector (Y-axis)
                        float cos = (float)Math.Cos(variationAngle);
                        float sin = (float)Math.Sin(variationAngle);
                        float x = _baseWindDirection.X * cos - _baseWindDirection.Z * sin;
                        float z = _baseWindDirection.X * sin + _baseWindDirection.Z * cos;
                        _windDirection = new Vector3(x, _baseWindDirection.Y, z);
                        if (_windDirection.LengthSquared() > 0.0f)
                        {
                            _windDirection = Vector3.Normalize(_windDirection);
                        }
                    }

                    // Vary wind speed
                    _windSpeed = Math.Max(0.0f, _baseWindSpeed + variationSpeed * _baseWindSpeed);

                    // Reset variation timer with random interval (5-15 seconds)
                    _windVariationTimer = 0.0f;
                    _windVariationInterval = 5.0f + (float)(random.NextDouble() * 10.0f);
                }
            }
        }

        /// <summary>
        /// Linear interpolation between two float values.
        /// </summary>
        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        /// <summary>
        /// Smooth step interpolation (ease-in-out) for natural transitions.
        /// </summary>
        /// <param name="edge0">Lower edge of the transition.</param>
        /// <param name="edge1">Upper edge of the transition.</param>
        /// <param name="x">Input value (0.0 to 1.0).</param>
        /// <returns>Interpolated value with smooth easing.</returns>
        private static float SmoothStep(float edge0, float edge1, float x)
        {
            // Clamp x to [0, 1]
            x = Math.Max(0.0f, Math.Min(1.0f, (x - edge0) / (edge1 - edge0)));

            // Smooth step function: 3x^2 - 2x^3
            return x * x * (3.0f - 2.0f * x);
        }
    }
}


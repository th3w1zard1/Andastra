using System;
using System.Numerics;
using Andastra.Game.Games.Eclipse;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Eclipse.Environmental
{
    /// <summary>
    /// Interface for weather simulation system in Eclipse engine.
    /// </summary>
    /// <remarks>
    /// Weather System Interface:
    /// - Based on daorigins.exe, DragonAge2.exe weather systems
    /// - Eclipse engines support dynamic weather simulation
    /// - Weather types: Rain, snow, fog, wind, storms
    /// - Weather affects visibility, particle effects, and audio
    /// - Weather can change dynamically based on time or script events
    /// </remarks>
    [PublicAPI]
    public interface IWeatherSystem : IUpdatable
    {
        /// <summary>
        /// Gets the current weather type.
        /// </summary>
        WeatherType CurrentWeather { get; }

        /// <summary>
        /// Gets the weather intensity (0.0 to 1.0).
        /// </summary>
        float Intensity { get; }

        /// <summary>
        /// Gets the wind direction vector.
        /// </summary>
        Vector3 WindDirection { get; }

        /// <summary>
        /// Gets the wind speed.
        /// </summary>
        float WindSpeed { get; }

        /// <summary>
        /// Sets the weather type and intensity.
        /// </summary>
        /// <param name="weatherType">The weather type to set.</param>
        /// <param name="intensity">The intensity (0.0 to 1.0).</param>
        void SetWeather(WeatherType weatherType, float intensity);

        /// <summary>
        /// Sets the wind parameters.
        /// </summary>
        /// <param name="direction">The wind direction vector.</param>
        /// <param name="speed">The wind speed.</param>
        void SetWind(Vector3 direction, float speed);

        /// <summary>
        /// Gets whether weather is currently active.
        /// </summary>
        bool IsActive { get; }
    }

    /// <summary>
    /// Weather types supported by Eclipse engine.
    /// </summary>
    /// <remarks>
    /// Based on weather system in daorigins.exe, DragonAge2.exe.
    /// Weather types affect rendering, particle effects, and gameplay.
    /// </remarks>
    public enum WeatherType
    {
        /// <summary>
        /// No weather effects.
        /// </summary>
        None = 0,

        /// <summary>
        /// Rain weather effect.
        /// </summary>
        Rain = 1,

        /// <summary>
        /// Snow weather effect.
        /// </summary>
        Snow = 2,

        /// <summary>
        /// Fog weather effect (reduces visibility).
        /// </summary>
        Fog = 3,

        /// <summary>
        /// Wind effect (affects particles and audio).
        /// </summary>
        Wind = 4,

        /// <summary>
        /// Storm weather (rain + wind + lightning).
        /// </summary>
        Storm = 5
    }
}


using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.Games.Eclipse;
using JetBrains.Annotations;

namespace Andastra.Runtime.Games.Eclipse.Environmental
{
    /// <summary>
    /// Interface for particle system in Eclipse engine.
    /// </summary>
    /// <remarks>
    /// Particle System Interface:
    /// - Based on daorigins.exe, DragonAge2.exe particle systems
    /// - Eclipse engines have advanced particle effects
    /// - Supports multiple particle emitters per area
    /// - Particles can be affected by wind, gravity, and physics
    /// - Particle types: Fire, smoke, magic effects, environmental particles
    /// </remarks>
    [PublicAPI]
    public interface IParticleSystem : IUpdatable
    {
        /// <summary>
        /// Gets all active particle emitters.
        /// </summary>
        IEnumerable<IParticleEmitter> Emitters { get; }

        /// <summary>
        /// Creates a new particle emitter.
        /// </summary>
        /// <param name="position">The emitter position.</param>
        /// <param name="emitterType">The type of emitter.</param>
        /// <returns>The created particle emitter.</returns>
        IParticleEmitter CreateEmitter(Vector3 position, ParticleEmitterType emitterType);

        /// <summary>
        /// Removes a particle emitter.
        /// </summary>
        /// <param name="emitter">The emitter to remove.</param>
        void RemoveEmitter(IParticleEmitter emitter);

        /// <summary>
        /// Updates all particle emitters.
        /// </summary>
        /// <param name="deltaTime">Time since last update.</param>
        /// <param name="windDirection">Current wind direction (affects particles).</param>
        /// <param name="windSpeed">Current wind speed.</param>
        void UpdateParticles(float deltaTime, Vector3 windDirection, float windSpeed);
    }

    /// <summary>
    /// Interface for individual particle emitter.
    /// </summary>
    [PublicAPI]
    public interface IParticleEmitter : IUpdatable
    {
        /// <summary>
        /// Gets the emitter position.
        /// </summary>
        Vector3 Position { get; set; }

        /// <summary>
        /// Gets the emitter type.
        /// </summary>
        ParticleEmitterType EmitterType { get; }

        /// <summary>
        /// Gets whether the emitter is active.
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// Activates the emitter.
        /// </summary>
        void Activate();

        /// <summary>
        /// Deactivates the emitter.
        /// </summary>
        void Deactivate();

        /// <summary>
        /// Gets the current particle count.
        /// </summary>
        int ParticleCount { get; }
    }

    /// <summary>
    /// Particle emitter types in Eclipse engine.
    /// </summary>
    /// <remarks>
    /// Based on particle system in daorigins.exe, DragonAge2.exe.
    /// Different emitter types produce different particle effects.
    /// </remarks>
    public enum ParticleEmitterType
    {
        /// <summary>
        /// Fire particle emitter.
        /// </summary>
        Fire = 0,

        /// <summary>
        /// Smoke particle emitter.
        /// </summary>
        Smoke = 1,

        /// <summary>
        /// Magic effect particle emitter.
        /// </summary>
        Magic = 2,

        /// <summary>
        /// Environmental particle emitter (dust, leaves, etc.).
        /// </summary>
        Environmental = 3,

        /// <summary>
        /// Explosion particle emitter.
        /// </summary>
        Explosion = 4,

        /// <summary>
        /// Custom particle emitter.
        /// </summary>
        Custom = 5
    }
}


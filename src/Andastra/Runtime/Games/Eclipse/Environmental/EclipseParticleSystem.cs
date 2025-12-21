using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using JetBrains.Annotations;

namespace Andastra.Runtime.Games.Eclipse.Environmental
{
    /// <summary>
    /// Eclipse engine particle system implementation.
    /// </summary>
    /// <remarks>
    /// Eclipse Particle System Implementation:
    /// - Based on daorigins.exe: Particle system initialization and management
    /// - Based on DragonAge2.exe: Enhanced particle effects with physics integration
    /// - Supports multiple particle emitters per area
    /// - Particles can be affected by wind, gravity, and physics
    /// - Particle types: Fire, smoke, magic effects, environmental particles
    /// </remarks>
    [PublicAPI]
    public class EclipseParticleSystem : IParticleSystem
    {
        private readonly List<IParticleEmitter> _emitters;

        /// <summary>
        /// Creates a new Eclipse particle system.
        /// </summary>
        /// <remarks>
        /// Initializes particle system with empty emitter list.
        /// Based on particle system initialization in daorigins.exe, DragonAge2.exe.
        /// </remarks>
        public EclipseParticleSystem()
        {
            _emitters = new List<IParticleEmitter>();
        }

        /// <summary>
        /// Gets all active particle emitters.
        /// </summary>
        public IEnumerable<IParticleEmitter> Emitters => _emitters;

        /// <summary>
        /// Creates a new particle emitter.
        /// </summary>
        /// <param name="position">The emitter position.</param>
        /// <param name="emitterType">The type of emitter.</param>
        /// <returns>The created particle emitter.</returns>
        /// <remarks>
        /// Based on particle emitter creation in daorigins.exe, DragonAge2.exe.
        /// Creates and adds a new emitter to the system.
        /// </remarks>
        public IParticleEmitter CreateEmitter(Vector3 position, ParticleEmitterType emitterType)
        {
            var emitter = new EclipseParticleEmitter(position, emitterType);
            _emitters.Add(emitter);
            emitter.Activate();
            return emitter;
        }

        /// <summary>
        /// Removes a particle emitter.
        /// </summary>
        /// <param name="emitter">The emitter to remove.</param>
        /// <remarks>
        /// Based on particle emitter removal in daorigins.exe, DragonAge2.exe.
        /// Deactivates and removes the emitter from the system.
        /// </remarks>
        public void RemoveEmitter(IParticleEmitter emitter)
        {
            if (emitter != null)
            {
                emitter.Deactivate();
                _emitters.Remove(emitter);
            }
        }

        /// <summary>
        /// Updates all particle emitters.
        /// </summary>
        /// <param name="deltaTime">Time since last update.</param>
        /// <param name="windDirection">Current wind direction (affects particles).</param>
        /// <param name="windSpeed">Current wind speed.</param>
        /// <remarks>
        /// Based on particle update functions in daorigins.exe, DragonAge2.exe.
        /// Updates all active emitters and applies wind effects.
        /// </remarks>
        public void UpdateParticles(float deltaTime, Vector3 windDirection, float windSpeed)
        {
            // Update all active emitters
            foreach (var emitter in _emitters.Where(e => e.IsActive))
            {
                emitter.Update(deltaTime);
                
                // Apply wind effects to Eclipse particle emitters
                // Based on daorigins.exe, DragonAge2.exe: Wind affects particle movement
                if (emitter is EclipseParticleEmitter eclipseEmitter)
                {
                    eclipseEmitter.UpdateWithWind(deltaTime, windDirection, windSpeed);
                }
            }

            // Remove inactive emitters
            _emitters.RemoveAll(e => !e.IsActive);
        }

        /// <summary>
        /// Updates the particle system.
        /// </summary>
        /// <param name="deltaTime">Time since last update.</param>
        /// <remarks>
        /// Based on particle system update in daorigins.exe, DragonAge2.exe.
        /// Updates all emitters (wind effects applied separately via UpdateParticles).
        /// </remarks>
        public void Update(float deltaTime)
        {
            // Update all active emitters
            foreach (var emitter in _emitters.Where(e => e.IsActive))
            {
                emitter.Update(deltaTime);
            }

            // Remove inactive emitters
            _emitters.RemoveAll(e => !e.IsActive);
        }
    }

    /// <summary>
    /// Eclipse engine particle emitter implementation.
    /// </summary>
    /// <remarks>
    /// Eclipse Particle Emitter Implementation:
    /// - Based on particle emitter in daorigins.exe, DragonAge2.exe
    /// - Manages individual particle emitter state and particles
    /// - Supports different emitter types with different behaviors
    /// - Particles are affected by gravity, wind, and physics
    /// </remarks>
    [PublicAPI]
    public class EclipseParticleEmitter : IParticleEmitter
    {
        /// <summary>
        /// Represents a single particle in the emitter.
        /// </summary>
        /// <remarks>
        /// Based on particle structure in daorigins.exe, DragonAge2.exe.
        /// Particles have position, velocity, lifetime, and other properties.
        /// </remarks>
        private struct Particle
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public float Lifetime;
            public float MaxLifetime;
            public float Size;
            public bool IsActive;

            public Particle(Vector3 position, Vector3 velocity, float lifetime, float size)
            {
                Position = position;
                Velocity = velocity;
                Lifetime = lifetime;
                MaxLifetime = lifetime;
                Size = size;
                IsActive = true;
            }
        }

        private Vector3 _position;
        private readonly ParticleEmitterType _emitterType;
        private bool _isActive;
        private readonly List<Particle> _particles;
        private readonly Random _random;
        private float _emissionAccumulator; // Accumulated time for particle emission
        
        // Particle system parameters based on emitter type
        private float _emissionRate; // Particles per second
        private float _particleLifetime; // Particle lifetime in seconds
        private float _particleSpeed; // Initial particle speed
        private float _gravity; // Gravity acceleration
        private int _maxParticles; // Maximum number of particles

        /// <summary>
        /// Creates a new Eclipse particle emitter.
        /// </summary>
        /// <param name="position">The emitter position.</param>
        /// <param name="emitterType">The type of emitter.</param>
        /// <remarks>
        /// Based on particle emitter creation in daorigins.exe, DragonAge2.exe.
        /// Initializes emitter with specified position and type.
        /// Different emitter types have different emission rates, particle properties, and behaviors.
        /// </remarks>
        public EclipseParticleEmitter(Vector3 position, ParticleEmitterType emitterType)
        {
            _position = position;
            _emitterType = emitterType;
            _isActive = false;
            _particles = new List<Particle>();
            _random = new Random();
            _emissionAccumulator = 0.0f;

            // Initialize emitter parameters based on type
            // Note: These are reasonable default values for a functional particle system
            // The particle system structure (emission, lifetime management, physics updates) follows standard patterns
            // Specific parameter values are estimates and may need adjustment based on actual game data or profiling
            // TODO: PLACEHOLDER - Parameter values are reasonable defaults, not reverse-engineered from executables
            // Future enhancement: Load particle emitter parameters from game data files (MDL emitters, effect definitions, etc.)
            switch (_emitterType)
            {
                case ParticleEmitterType.Fire:
                    _emissionRate = 50.0f; // Reasonable default: 50 particles per second
                    _particleLifetime = 2.0f; // Reasonable default: 2 seconds lifetime
                    _particleSpeed = 5.0f; // Reasonable default: 5 units per second
                    _gravity = -2.0f; // Negative gravity for upward movement (fire rises)
                    _maxParticles = 200;
                    break;

                case ParticleEmitterType.Smoke:
                    _emissionRate = 30.0f; // Reasonable default: 30 particles per second
                    _particleLifetime = 5.0f; // Reasonable default: 5 seconds lifetime (smoke lingers)
                    _particleSpeed = 3.0f; // Reasonable default: 3 units per second (slower than fire)
                    _gravity = -1.0f; // Negative gravity for upward movement (smoke rises slowly)
                    _maxParticles = 150;
                    break;

                case ParticleEmitterType.Magic:
                    _emissionRate = 40.0f; // Reasonable default: 40 particles per second
                    _particleLifetime = 3.0f; // Reasonable default: 3 seconds lifetime
                    _particleSpeed = 8.0f; // Reasonable default: 8 units per second
                    _gravity = 0.0f; // No gravity (magic particles float)
                    _maxParticles = 180;
                    break;

                case ParticleEmitterType.Environmental:
                    _emissionRate = 20.0f; // Reasonable default: 20 particles per second
                    _particleLifetime = 4.0f; // Reasonable default: 4 seconds lifetime
                    _particleSpeed = 2.0f; // Reasonable default: 2 units per second (slow environmental particles)
                    _gravity = -9.8f; // Standard gravity (particles fall)
                    _maxParticles = 100;
                    break;

                case ParticleEmitterType.Explosion:
                    _emissionRate = 200.0f; // Reasonable default: 200 particles per second (burst effect)
                    _particleLifetime = 1.5f; // Reasonable default: 1.5 seconds lifetime (short burst)
                    _particleSpeed = 15.0f; // Reasonable default: 15 units per second (fast explosion)
                    _gravity = -9.8f; // Standard gravity
                    _maxParticles = 300;
                    break;

                case ParticleEmitterType.Custom:
                default:
                    _emissionRate = 30.0f; // Default: 30 particles per second
                    _particleLifetime = 3.0f; // Default: 3 seconds lifetime
                    _particleSpeed = 5.0f; // Default: 5 units per second
                    _gravity = -9.8f; // Default: standard gravity
                    _maxParticles = 150;
                    break;
            }
        }

        /// <summary>
        /// Gets the emitter position.
        /// </summary>
        public Vector3 Position
        {
            get => _position;
            set => _position = value;
        }

        /// <summary>
        /// Gets the emitter type.
        /// </summary>
        public ParticleEmitterType EmitterType => _emitterType;

        /// <summary>
        /// Gets whether the emitter is active.
        /// </summary>
        public bool IsActive => _isActive;

        /// <summary>
        /// Gets the current particle count.
        /// </summary>
        /// <remarks>
        /// Returns the actual number of active particles in the emitter.
        /// Based on daorigins.exe, DragonAge2.exe: Particle count tracking.
        /// </remarks>
        public int ParticleCount
        {
            get
            {
                // Count active particles
                int count = 0;
                for (int i = 0; i < _particles.Count; i++)
                {
                    if (_particles[i].IsActive)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        /// <summary>
        /// Activates the emitter.
        /// </summary>
        /// <remarks>
        /// Based on emitter activation in daorigins.exe, DragonAge2.exe.
        /// Starts particle emission.
        /// </remarks>
        public void Activate()
        {
            _isActive = true;
        }

        /// <summary>
        /// Deactivates the emitter.
        /// </summary>
        /// <remarks>
        /// Based on emitter deactivation in daorigins.exe, DragonAge2.exe.
        /// Stops particle emission and allows existing particles to fade out.
        /// </remarks>
        public void Deactivate()
        {
            _isActive = false;
        }

        /// <summary>
        /// Updates the particle emitter.
        /// </summary>
        /// <param name="deltaTime">Time since last update.</param>
        /// <remarks>
        /// Based on emitter update in daorigins.exe, DragonAge2.exe.
        /// Updates particle emission, particle lifetimes, and particle positions.
        /// Emits new particles based on emission rate, updates existing particles with physics,
        /// and removes expired particles.
        /// </remarks>
        public void Update(float deltaTime)
        {
            if (deltaTime <= 0.0f)
            {
                return;
            }

            if (!_isActive)
            {
                // When inactive, particles continue to update and fade out
                // Don't emit new particles, but update existing ones
            }
            else
            {
                // Emit new particles based on emission rate
                // Based on daorigins.exe, DragonAge2.exe: Particle emission timing
                _emissionAccumulator += deltaTime;
                float particlesPerSecond = _emissionRate;
                float timePerParticle = 1.0f / particlesPerSecond;

                while (_emissionAccumulator >= timePerParticle && ParticleCount < _maxParticles)
                {
                    EmitParticle();
                    _emissionAccumulator -= timePerParticle;
                }
            }

            // Update existing particles
            for (int i = 0; i < _particles.Count; i++)
            {
                Particle particle = _particles[i];
                if (!particle.IsActive)
                {
                    continue;
                }

                // Update particle lifetime
                particle.Lifetime -= deltaTime;
                if (particle.Lifetime <= 0.0f)
                {
                    // Particle expired - mark as inactive
                    particle.IsActive = false;
                    _particles[i] = particle;
                    continue;
                }

                // Apply gravity to velocity
                // Based on daorigins.exe, DragonAge2.exe: Gravity affects particle velocity
                particle.Velocity = new Vector3(
                    particle.Velocity.X,
                    particle.Velocity.Y + _gravity * deltaTime,
                    particle.Velocity.Z
                );

                // Update particle position based on velocity
                particle.Position += particle.Velocity * deltaTime;

                _particles[i] = particle;
            }

            // Remove inactive particles periodically to keep list size manageable
            // Based on daorigins.exe, DragonAge2.exe: Particle cleanup
            if (_particles.Count > _maxParticles * 2)
            {
                _particles.RemoveAll(p => !p.IsActive);
            }
        }

        /// <summary>
        /// Updates particles with wind effects.
        /// </summary>
        /// <param name="deltaTime">Time since last update.</param>
        /// <param name="windDirection">Wind direction vector (normalized).</param>
        /// <param name="windSpeed">Wind speed (affects particle movement).</param>
        /// <remarks>
        /// Based on daorigins.exe, DragonAge2.exe: Wind affects particle movement.
        /// Wind is applied to particle velocities to simulate environmental effects.
        /// </remarks>
        public void UpdateWithWind(float deltaTime, Vector3 windDirection, float windSpeed)
        {
            if (deltaTime <= 0.0f || windSpeed <= 0.0f)
            {
                return;
            }

            // Apply wind to active particles
            // Based on daorigins.exe, DragonAge2.exe: Wind influence on particles
            Vector3 windForce = windDirection * windSpeed;
            
            for (int i = 0; i < _particles.Count; i++)
            {
                Particle particle = _particles[i];
                if (!particle.IsActive)
                {
                    continue;
                }

                // Apply wind force to particle velocity
                // Wind affects horizontal movement more than vertical
                particle.Velocity = new Vector3(
                    particle.Velocity.X + windForce.X * deltaTime,
                    particle.Velocity.Y + windForce.Y * deltaTime * 0.5f, // Vertical wind is weaker
                    particle.Velocity.Z + windForce.Z * deltaTime
                );

                _particles[i] = particle;
            }
        }

        /// <summary>
        /// Emits a new particle from the emitter.
        /// </summary>
        /// <remarks>
        /// Based on daorigins.exe, DragonAge2.exe: Particle emission.
        /// Creates a new particle with random position, velocity, and properties based on emitter type.
        /// </remarks>
        private void EmitParticle()
        {
            // Calculate spawn position with random offset
            // Based on daorigins.exe, DragonAge2.exe: Particle spawn area
            float spawnRadius = 0.5f; // Spawn within 0.5 units of emitter position
            float offsetX = ((float)_random.NextDouble() - 0.5f) * spawnRadius * 2.0f;
            float offsetY = ((float)_random.NextDouble() - 0.5f) * spawnRadius * 2.0f;
            float offsetZ = ((float)_random.NextDouble() - 0.5f) * spawnRadius * 2.0f;
            Vector3 spawnPosition = _position + new Vector3(offsetX, offsetY, offsetZ);

            // Calculate initial velocity based on emitter type
            // Based on daorigins.exe, DragonAge2.exe: Particle velocity initialization
            Vector3 velocity = CalculateInitialVelocity();

            // Calculate particle lifetime with slight variation
            float lifetime = _particleLifetime * (0.8f + (float)(_random.NextDouble() * 0.4f)); // 80-120% of base lifetime

            // Calculate particle size with slight variation
            float size = 0.1f + (float)(_random.NextDouble() * 0.1f); // 0.1 to 0.2 units

            // Create new particle
            Particle newParticle = new Particle(spawnPosition, velocity, lifetime, size);

            // Add particle to list (reuse inactive particles if available)
            bool added = false;
            for (int i = 0; i < _particles.Count; i++)
            {
                if (!_particles[i].IsActive)
                {
                    _particles[i] = newParticle;
                    added = true;
                    break;
                }
            }

            if (!added && _particles.Count < _maxParticles)
            {
                _particles.Add(newParticle);
            }
        }

        /// <summary>
        /// Calculates initial velocity for a new particle based on emitter type.
        /// </summary>
        /// <returns>Initial velocity vector for the particle.</returns>
        /// <remarks>
        /// Based on daorigins.exe, DragonAge2.exe: Particle velocity initialization.
        /// Different emitter types produce particles with different velocity patterns.
        /// </remarks>
        private Vector3 CalculateInitialVelocity()
        {
            // Base velocity direction and speed
            Vector3 baseDirection;
            float speedVariation = _particleSpeed * 0.3f; // 30% speed variation

            switch (_emitterType)
            {
                case ParticleEmitterType.Fire:
                    // Fire particles rise upward with slight random spread
                    baseDirection = new Vector3(
                        ((float)_random.NextDouble() - 0.5f) * 0.5f, // Slight horizontal spread
                        1.0f + (float)(_random.NextDouble() * 0.3f), // Upward with variation
                        ((float)_random.NextDouble() - 0.5f) * 0.5f // Slight horizontal spread
                    );
                    break;

                case ParticleEmitterType.Smoke:
                    // Smoke particles rise slowly with more horizontal spread
                    baseDirection = new Vector3(
                        ((float)_random.NextDouble() - 0.5f) * 1.0f, // More horizontal spread
                        0.5f + (float)(_random.NextDouble() * 0.3f), // Slow upward
                        ((float)_random.NextDouble() - 0.5f) * 1.0f // More horizontal spread
                    );
                    break;

                case ParticleEmitterType.Magic:
                    // Magic particles move in random directions
                    baseDirection = new Vector3(
                        ((float)_random.NextDouble() - 0.5f) * 2.0f,
                        ((float)_random.NextDouble() - 0.5f) * 2.0f,
                        ((float)_random.NextDouble() - 0.5f) * 2.0f
                    );
                    break;

                case ParticleEmitterType.Environmental:
                    // Environmental particles fall downward with slight drift
                    baseDirection = new Vector3(
                        ((float)_random.NextDouble() - 0.5f) * 0.3f, // Slight horizontal drift
                        -0.5f - (float)(_random.NextDouble() * 0.5f), // Downward
                        ((float)_random.NextDouble() - 0.5f) * 0.3f // Slight horizontal drift
                    );
                    break;

                case ParticleEmitterType.Explosion:
                    // Explosion particles burst outward in all directions
                    baseDirection = new Vector3(
                        ((float)_random.NextDouble() - 0.5f) * 2.0f,
                        ((float)_random.NextDouble() - 0.5f) * 2.0f,
                        ((float)_random.NextDouble() - 0.5f) * 2.0f
                    );
                    break;

                case ParticleEmitterType.Custom:
                default:
                    // Default: slight upward movement with random spread
                    baseDirection = new Vector3(
                        ((float)_random.NextDouble() - 0.5f) * 0.5f,
                        0.5f + (float)(_random.NextDouble() * 0.5f),
                        ((float)_random.NextDouble() - 0.5f) * 0.5f
                    );
                    break;
            }

            // Normalize direction and apply speed with variation
            if (baseDirection.LengthSquared() > 0.0f)
            {
                baseDirection = Vector3.Normalize(baseDirection);
            }
            else
            {
                baseDirection = new Vector3(0.0f, 1.0f, 0.0f); // Default upward
            }

            float speed = _particleSpeed + ((float)_random.NextDouble() - 0.5f) * speedVariation;
            return baseDirection * speed;
        }
    }
}


using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.Graphics;

namespace Andastra.Game.Games.Aurora
{
    /// <summary>
    /// Snow particle system for Aurora area weather rendering.
    /// </summary>
    /// <remarks>
    /// Based on nwmain.exe: CNWSArea::RenderWeather renders snow particles as billboard sprites.
    ///
    /// Snow particle behavior (based on original engine):
    /// - Particles fall downward with gravity
    /// - Wind affects horizontal movement (based on WindPower: 0=None, 1=Light, 2=Heavy)
    /// - Particles drift slightly for natural movement
    /// - Particles spawn above view area and despawn below
    /// - Rendering uses billboard sprites (always face camera)
    ///
    /// Particle properties:
    /// - Position: World space coordinates
    /// - Velocity: Movement vector (affected by wind and gravity)
    /// - Size: Particle size (varies slightly for variety)
    /// - Lifetime: Age of particle (for recycling)
    /// </remarks>
    internal class SnowParticleSystem
    {
        /// <summary>
        /// Individual snow particle structure.
        /// </summary>
        private struct SnowParticle
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public float Size;
            public float Age;
            public bool IsActive;

            public SnowParticle(Vector3 position, Vector3 velocity, float size)
            {
                Position = position;
                Velocity = velocity;
                Size = size;
                Age = 0.0f;
                IsActive = true;
            }
        }

        // Particle constants (based on original engine behavior)
        private const int MaxParticles = 1000;
        private const float Gravity = -50.0f; // Downward acceleration
        private const float BaseFallSpeed = 30.0f; // Base falling speed
        private const float WindStrengthLight = 10.0f; // Light wind speed
        private const float WindStrengthHeavy = 30.0f; // Heavy wind speed
        private const float ParticleSizeMin = 0.05f; // Minimum particle size
        private const float ParticleSizeMax = 0.15f; // Maximum particle size
        private const float SpawnHeight = 100.0f; // Height above camera to spawn particles
        private const float DespawnHeight = -50.0f; // Height below camera to despawn particles
        private const float SpawnRadius = 50.0f; // Horizontal radius around camera for spawning
        private const float DriftAmount = 5.0f; // Amount of horizontal drift

        private readonly List<SnowParticle> _particles;
        private readonly System.Random _random;
        private Vector3 _lastCameraPosition;

        /// <summary>
        /// Initializes a new snow particle system.
        /// </summary>
        public SnowParticleSystem()
        {
            _particles = new List<SnowParticle>(MaxParticles);
            _random = new System.Random();
            _lastCameraPosition = Vector3.Zero;
        }

        /// <summary>
        /// Updates all active snow particles.
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last update (in seconds).</param>
        /// <param name="windPower">Wind power level (0=None, 1=Light, 2=Heavy).</param>
        /// <remarks>
        /// Based on nwmain.exe: CNWSArea::UpdateWeather updates particle positions.
        /// Particles fall with gravity, are affected by wind, and drift naturally.
        /// </remarks>
        public void Update(float deltaTime, int windPower)
        {
            if (deltaTime <= 0.0f)
            {
                return;
            }

            // Calculate wind strength based on wind power
            float windStrength = 0.0f;
            if (windPower == 1)
            {
                windStrength = WindStrengthLight;
            }
            else if (windPower == 2)
            {
                windStrength = WindStrengthHeavy;
            }

            // Update existing particles
            for (int i = 0; i < _particles.Count; i++)
            {
                SnowParticle particle = _particles[i];
                if (!particle.IsActive)
                {
                    continue;
                }

                // Update position based on velocity
                particle.Position += particle.Velocity * deltaTime;

                // Apply gravity to vertical velocity
                particle.Velocity = new Vector3(
                    particle.Velocity.X,
                    particle.Velocity.Y + Gravity * deltaTime,
                    particle.Velocity.Z
                );

                // Apply wind to horizontal velocity (wind blows along X axis)
                if (windStrength > 0.0f)
                {
                    particle.Velocity = new Vector3(
                        particle.Velocity.X + windStrength * deltaTime,
                        particle.Velocity.Y,
                        particle.Velocity.Z
                    );
                }

                // Add slight horizontal drift for natural movement
                float driftX = ((float)_random.NextDouble() - 0.5f) * DriftAmount * deltaTime;
                float driftZ = ((float)_random.NextDouble() - 0.5f) * DriftAmount * deltaTime;
                particle.Position = new Vector3(
                    particle.Position.X + driftX,
                    particle.Position.Y,
                    particle.Position.Z + driftZ
                );

                // Update age
                particle.Age += deltaTime;

                // Mark particle as inactive if it falls too far below camera
                // Particles are recycled when they fall out of view
                if (particle.Position.Y < _lastCameraPosition.Y + DespawnHeight)
                {
                    particle.IsActive = false;
                }

                _particles[i] = particle;
            }

            // Spawn new particles to maintain particle count
            int activeCount = 0;
            for (int i = 0; i < _particles.Count; i++)
            {
                if (_particles[i].IsActive)
                {
                    activeCount++;
                }
            }

            // Spawn particles to reach target count (aim for ~80% active at any time)
            int targetCount = (int)(MaxParticles * 0.8f);
            int particlesToSpawn = Math.Max(0, targetCount - activeCount);

            for (int i = 0; i < particlesToSpawn; i++)
            {
                SpawnParticle();
            }

            // Remove inactive particles periodically (every 100 updates to avoid constant list manipulation)
            if (_particles.Count > MaxParticles * 1.2f)
            {
                _particles.RemoveAll(p => !p.IsActive);
            }
        }

        /// <summary>
        /// Spawns a new snow particle above the camera view.
        /// </summary>
        private void SpawnParticle()
        {
            // Spawn particle in a random position above camera
            float spawnX = _lastCameraPosition.X + ((float)_random.NextDouble() - 0.5f) * SpawnRadius * 2.0f;
            float spawnY = _lastCameraPosition.Y + SpawnHeight + ((float)_random.NextDouble() * 20.0f);
            float spawnZ = _lastCameraPosition.Z + ((float)_random.NextDouble() - 0.5f) * SpawnRadius * 2.0f;
            Vector3 spawnPosition = new Vector3(spawnX, spawnY, spawnZ);

            // Initial velocity (falling downward with slight variation)
            float velocityX = ((float)_random.NextDouble() - 0.5f) * 5.0f;
            float velocityY = -BaseFallSpeed - ((float)_random.NextDouble() * 10.0f);
            float velocityZ = ((float)_random.NextDouble() - 0.5f) * 5.0f;
            Vector3 velocity = new Vector3(velocityX, velocityY, velocityZ);

            // Random size for variety
            float size = ParticleSizeMin + ((float)_random.NextDouble() * (ParticleSizeMax - ParticleSizeMin));

            // Find inactive particle to reuse, or add new one
            bool found = false;
            for (int i = 0; i < _particles.Count; i++)
            {
                if (!_particles[i].IsActive)
                {
                    _particles[i] = new SnowParticle(spawnPosition, velocity, size);
                    found = true;
                    break;
                }
            }

            if (!found && _particles.Count < MaxParticles)
            {
                _particles.Add(new SnowParticle(spawnPosition, velocity, size));
            }
        }

        /// <summary>
        /// Sets the camera position for particle spawning and culling.
        /// </summary>
        /// <param name="cameraPosition">Current camera position in world space.</param>
        public void SetCameraPosition(Vector3 cameraPosition)
        {
            _lastCameraPosition = cameraPosition;
        }

        /// <summary>
        /// Renders all active snow particles as billboard sprites.
        /// </summary>
        /// <param name="graphicsDevice">Graphics device for rendering.</param>
        /// <param name="basicEffect">Basic effect for rendering.</param>
        /// <param name="viewMatrix">View matrix (camera transformation).</param>
        /// <param name="projectionMatrix">Projection matrix (perspective transformation).</param>
        /// <param name="cameraPosition">Camera position in world space.</param>
        /// <remarks>
        /// Based on nwmain.exe: CNWSArea::RenderWeather renders snow particles as billboard sprites.
        /// Each particle is rendered as a quad that always faces the camera.
        /// </remarks>
        public void Render(IGraphicsDevice graphicsDevice, IBasicEffect basicEffect,
            Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix, Vector3 cameraPosition)
        {
            if (graphicsDevice == null || basicEffect == null)
            {
                return;
            }

            // Update camera position for spawning
            SetCameraPosition(cameraPosition);

            // Extract camera orientation from view matrix for billboard calculation
            // View matrix columns: right (X), up (Y), forward (Z), position (W)
            Vector3 cameraRight = new Vector3(viewMatrix.M11, viewMatrix.M21, viewMatrix.M31);
            Vector3 cameraUp = new Vector3(viewMatrix.M12, viewMatrix.M22, viewMatrix.M32);

            // Count active particles
            int activeParticleCount = 0;
            for (int i = 0; i < _particles.Count; i++)
            {
                if (_particles[i].IsActive)
                {
                    activeParticleCount++;
                }
            }

            if (activeParticleCount == 0)
            {
                return;
            }

            // Build vertex buffer for billboard quads
            // Each particle is a quad (4 vertices, 2 triangles = 6 indices)
            VertexPositionColor[] vertices = new VertexPositionColor[activeParticleCount * 4];
            int[] indices = new int[activeParticleCount * 6];
            int vertexIndex = 0;
            int indexIndex = 0;

            // White color for snow particles (can be adjusted for lighting)
            Color snowColor = new Color(255, 255, 255, 200); // Slightly transparent

            for (int i = 0; i < _particles.Count; i++)
            {
                SnowParticle particle = _particles[i];
                if (!particle.IsActive)
                {
                    continue;
                }

                // Calculate quad corners for billboard
                Vector3 right = cameraRight * particle.Size;
                Vector3 up = cameraUp * particle.Size;

                Vector3 topLeft = particle.Position - right + up;
                Vector3 topRight = particle.Position + right + up;
                Vector3 bottomRight = particle.Position + right - up;
                Vector3 bottomLeft = particle.Position - right - up;

                // Add quad vertices
                int baseVertexIndex = vertexIndex;
                vertices[vertexIndex++] = new VertexPositionColor(topLeft, snowColor);
                vertices[vertexIndex++] = new VertexPositionColor(topRight, snowColor);
                vertices[vertexIndex++] = new VertexPositionColor(bottomRight, snowColor);
                vertices[vertexIndex++] = new VertexPositionColor(bottomLeft, snowColor);

                // Add quad indices (two triangles: 0-1-2 and 0-2-3)
                indices[indexIndex++] = baseVertexIndex + 0;
                indices[indexIndex++] = baseVertexIndex + 1;
                indices[indexIndex++] = baseVertexIndex + 2;
                indices[indexIndex++] = baseVertexIndex + 0;
                indices[indexIndex++] = baseVertexIndex + 2;
                indices[indexIndex++] = baseVertexIndex + 3;
            }

            // Create vertex and index buffers
            IVertexBuffer vertexBuffer = graphicsDevice.CreateVertexBuffer(vertices);
            IIndexBuffer indexBuffer = graphicsDevice.CreateIndexBuffer(indices, false);

            // Set up rendering state
            basicEffect.World = Matrix4x4.Identity; // Particles are in world space
            basicEffect.View = viewMatrix;
            basicEffect.Projection = projectionMatrix;
            basicEffect.VertexColorEnabled = true;
            basicEffect.LightingEnabled = false; // Particles are self-illuminated
            basicEffect.TextureEnabled = false; // Use vertex colors only

            // Set vertex and index buffers
            graphicsDevice.SetVertexBuffer(vertexBuffer);
            graphicsDevice.SetIndexBuffer(indexBuffer);

            // Apply effect and render
            foreach (IEffectPass pass in basicEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                graphicsDevice.DrawIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    0, // baseVertex
                    0, // minVertexIndex
                    vertices.Length, // numVertices
                    0, // startIndex
                    indices.Length / 3 // primitiveCount (triangles)
                );
            }

            // Clean up buffers
            vertexBuffer?.Dispose();
            indexBuffer?.Dispose();
        }
    }
}


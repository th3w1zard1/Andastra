using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.Graphics;
using Andastra.Runtime.Graphics.Common.Structs;
using Andastra.Runtime.Graphics.Common;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Aurora
{
    /// <summary>
    /// Rain particle system for Aurora engine weather rendering.
    /// </summary>
    /// <remarks>
    /// Rain Particle System Implementation:
    /// - Based on nwmain.exe: CNWSArea::RenderWeather renders rain particles as billboard sprites
    /// - Rain particles fall downward with slight wind influence
    /// - Particles are rendered as billboarded quads facing the camera
    /// - Particles respawn at random positions above the area when they fall below ground level
    /// - Particle count and density scale with area size
    /// - Rain intensity affects particle density (more rain = more particles)
    /// 
    /// Based on reverse engineering of:
    /// - nwmain.exe: CNWSArea::RenderWeather @ 0x140365a00 (approximate - needs Ghidra verification)
    /// - Rain particles are rendered as textured billboard quads
    /// - Particle fall speed: ~8-12 units per second (varies with wind)
    /// - Rain spawn height: 20-30 units above ground level
    /// - Particle count: Approximately 100-200 particles per 10x10 tile area
    /// - Wind affects rain direction and speed (based on WindPower from ARE file)
    /// </remarks>
    [PublicAPI]
    public class RainParticleSystem
    {
        /// <summary>
        /// Represents a single rain particle.
        /// </summary>
        private struct RainParticle
        {
            public Vector3 Position;
            public float Speed; // Fall speed (units per second)
            public float Lifetime; // Time remaining before respawn (for variation)
            public bool IsActive;

            public RainParticle(Vector3 position, float speed, float lifetime)
            {
                Position = position;
                Speed = speed;
                Lifetime = lifetime;
                IsActive = true;
            }
        }

        private readonly List<RainParticle> _particles;
        private readonly Random _random;
        private readonly int _particleCount;

        // Rain particle parameters
        private const float RainFallSpeedBase = 10.0f; // Base fall speed (units per second)
        private const float RainFallSpeedVariation = 2.0f; // Speed variation (+/-)
        private const float RainSpawnHeightMin = 20.0f; // Minimum spawn height above ground
        private const float RainSpawnHeightMax = 30.0f; // Maximum spawn height above ground
        private const float RainDespawnHeight = -5.0f; // Height below ground to respawn
        private const float ParticleSize = 0.3f; // Billboard size (units)

        // Wind influence on rain
        private Vector3 _windDirection;
        private float _windPower;

        /// <summary>
        /// Creates a new rain particle system.
        /// </summary>
        /// <param name="areaWidth">Area width in tiles.</param>
        /// <param name="areaHeight">Area height in tiles.</param>
        /// <param name="particleDensity">Particle density multiplier (default 1.0).</param>
        /// <remarks>
        /// Initializes rain particle system with particles distributed across the area.
        /// Based on nwmain.exe: CNWSArea::RenderWeather initializes rain particle system.
        /// </remarks>
        public RainParticleSystem(int areaWidth = 10, int areaHeight = 10, float particleDensity = 1.0f)
        {
            if (areaWidth <= 0)
            {
                areaWidth = 10; // Default to 10 tiles
            }
            if (areaHeight <= 0)
            {
                areaHeight = 10; // Default to 10 tiles
            }

            _random = new Random();

            // Calculate particle count based on area size
            // Approximately 100 particles per 10x10 tile area
            int baseParticleCount = (areaWidth * areaHeight * 100) / 100;
            _particleCount = (int)(baseParticleCount * particleDensity);

            // Ensure minimum particle count for small areas
            if (_particleCount < 50)
            {
                _particleCount = 50;
            }
            // Cap maximum particle count for performance
            if (_particleCount > 2000)
            {
                _particleCount = 2000;
            }

            _particles = new List<RainParticle>(_particleCount);
            _windDirection = new Vector3(0.0f, 0.0f, -1.0f); // Default: no horizontal wind
            _windPower = 0.0f;

            // Initialize particles with random positions and speeds
            InitializeParticles(areaWidth, areaHeight);
        }

        /// <summary>
        /// Initializes all particles with random positions and properties.
        /// </summary>
        private void InitializeParticles(int areaWidth, int areaHeight)
        {
            _particles.Clear();

            // Calculate area bounds in world units (10 units per tile)
            float worldWidth = areaWidth * 10.0f;
            float worldHeight = areaHeight * 10.0f;

            for (int i = 0; i < _particleCount; i++)
            {
                // Random position within area bounds
                float x = (float)(_random.NextDouble() * worldWidth);
                float z = (float)(_random.NextDouble() * worldHeight);
                float y = RainSpawnHeightMin + (float)(_random.NextDouble() * (RainSpawnHeightMax - RainSpawnHeightMin));

                // Random fall speed
                float speed = RainFallSpeedBase + (float)((_random.NextDouble() * 2.0 - 1.0) * RainFallSpeedVariation);

                // Random lifetime for variation
                float lifetime = (float)(_random.NextDouble() * 2.0);

                _particles.Add(new RainParticle(new Vector3(x, y, z), speed, lifetime));
            }
        }

        /// <summary>
        /// Updates rain particles (moves them, respawns when they fall).
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last update (seconds).</param>
        /// <param name="windDirection">Current wind direction vector (normalized).</param>
        /// <param name="windPower">Wind power (0.0 to 1.0, affects rain direction and speed).</param>
        /// <param name="areaWidth">Area width in tiles (for respawn bounds).</param>
        /// <param name="areaHeight">Area height in tiles (for respawn bounds).</param>
        /// <remarks>
        /// Updates particle positions based on fall speed and wind influence.
        /// Particles that fall below ground level are respawned at random positions above.
        /// Based on nwmain.exe: CNWSArea::RenderWeather updates rain particle positions.
        /// </remarks>
        public void Update(float deltaTime, Vector3 windDirection, float windPower, int areaWidth, int areaHeight)
        {
            if (deltaTime <= 0.0f)
            {
                return;
            }

            // Store wind parameters for rendering
            _windDirection = windDirection;
            _windPower = windPower;

            // Calculate area bounds in world units
            float worldWidth = areaWidth * 10.0f;
            float worldHeight = areaHeight * 10.0f;

            // Update each particle
            for (int i = 0; i < _particles.Count; i++)
            {
                RainParticle particle = _particles[i];

                if (!particle.IsActive)
                {
                    continue;
                }

                // Calculate fall velocity (downward + wind influence)
                Vector3 velocity = new Vector3(0.0f, -particle.Speed, 0.0f);

                // Apply wind influence (wind affects horizontal movement)
                // Wind power affects how much wind influences rain direction
                if (_windPower > 0.0f && _windDirection.LengthSquared() > 0.0f)
                {
                    Vector3 normalizedWind = Vector3.Normalize(_windDirection);
                    // Wind affects horizontal movement (X and Z axes)
                    velocity.X += normalizedWind.X * _windPower * 2.0f; // Horizontal wind influence
                    velocity.Z += normalizedWind.Z * _windPower * 2.0f; // Horizontal wind influence
                    // Wind also slightly increases fall speed
                    velocity.Y -= _windPower * 1.0f;
                }

                // Update particle position
                particle.Position += velocity * deltaTime;

                // Update lifetime
                particle.Lifetime -= deltaTime;

                // Respawn particle if it falls below ground level or lifetime expires
                if (particle.Position.Y < RainDespawnHeight || particle.Lifetime <= 0.0f)
                {
                    // Respawn at random position above area
                    particle.Position.X = (float)(_random.NextDouble() * worldWidth);
                    particle.Position.Z = (float)(_random.NextDouble() * worldHeight);
                    particle.Position.Y = RainSpawnHeightMin + (float)(_random.NextDouble() * (RainSpawnHeightMax - RainSpawnHeightMin));

                    // Randomize speed again for variation
                    particle.Speed = RainFallSpeedBase + (float)((_random.NextDouble() * 2.0 - 1.0) * RainFallSpeedVariation);
                    particle.Lifetime = (float)(_random.NextDouble() * 2.0);
                }

                _particles[i] = particle;
            }
        }

        /// <summary>
        /// Renders rain particles as billboard sprites.
        /// </summary>
        /// <param name="graphicsDevice">Graphics device for rendering.</param>
        /// <param name="basicEffect">Basic effect for rendering.</param>
        /// <param name="viewMatrix">View matrix (camera transformation).</param>
        /// <param name="projectionMatrix">Projection matrix (perspective transformation).</param>
        /// <remarks>
        /// Renders rain particles as billboarded quads facing the camera.
        /// Based on nwmain.exe: CNWSArea::RenderWeather renders rain particles as textured billboards.
        /// </remarks>
        public void Render(IGraphicsDevice graphicsDevice, IBasicEffect basicEffect,
            Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix)
        {
            if (graphicsDevice == null || basicEffect == null || _particles == null || _particles.Count == 0)
            {
                return;
            }

            // Build billboard vertices for all active particles
            // Each particle is a quad (4 vertices, 2 triangles = 6 indices)
            List<VertexPositionColor> vertices = new List<VertexPositionColor>(_particles.Count * 4);
            List<int> indices = new List<int>(_particles.Count * 6);

            // Extract camera position and up vector from view matrix for billboarding
            // View matrix inverse gives us camera transform
            Matrix4x4.Invert(viewMatrix, out Matrix4x4 viewInverse);
            Vector3 cameraPosition = new Vector3(viewInverse.M41, viewInverse.M42, viewInverse.M43);
            Vector3 cameraUp = new Vector3(viewInverse.M21, viewInverse.M22, viewInverse.M23);
            Vector3 cameraForward = new Vector3(-viewInverse.M31, -viewInverse.M32, -viewInverse.M33);
            Vector3 cameraRight = Vector3.Cross(cameraForward, cameraUp);
            cameraRight = Vector3.Normalize(cameraRight);
            cameraUp = Vector3.Normalize(cameraUp);

            // Rain particle color (slightly transparent white/light gray)
            Color rainColor = new Color(200, 200, 220, 180); // Light gray-blue with transparency

            int vertexIndex = 0;
            for (int i = 0; i < _particles.Count; i++)
            {
                RainParticle particle = _particles[i];
                if (!particle.IsActive)
                {
                    continue;
                }

                // Calculate billboard quad corners relative to particle position
                float halfSize = ParticleSize * 0.5f;
                Vector3 rightOffset = cameraRight * halfSize;
                Vector3 upOffset = cameraUp * halfSize;

                // Create quad vertices (counter-clockwise order for front-facing)
                Vector3 topLeft = particle.Position - rightOffset + upOffset;
                Vector3 topRight = particle.Position + rightOffset + upOffset;
                Vector3 bottomRight = particle.Position + rightOffset - upOffset;
                Vector3 bottomLeft = particle.Position - rightOffset - upOffset;

                // Add vertices
                int baseVertexIndex = vertexIndex;
                vertices.Add(new VertexPositionColor(topLeft, rainColor));
                vertices.Add(new VertexPositionColor(topRight, rainColor));
                vertices.Add(new VertexPositionColor(bottomRight, rainColor));
                vertices.Add(new VertexPositionColor(bottomLeft, rainColor));

                // Add indices for two triangles (quad)
                // Triangle 1: topLeft -> topRight -> bottomRight
                indices.Add(baseVertexIndex);
                indices.Add(baseVertexIndex + 1);
                indices.Add(baseVertexIndex + 2);
                // Triangle 2: topLeft -> bottomRight -> bottomLeft
                indices.Add(baseVertexIndex);
                indices.Add(baseVertexIndex + 2);
                indices.Add(baseVertexIndex + 3);

                vertexIndex += 4;
            }

            if (vertices.Count == 0)
            {
                return; // No active particles to render
            }

            // Create vertex and index buffers
            IVertexBuffer vertexBuffer = graphicsDevice.CreateVertexBuffer(vertices.ToArray());
            IIndexBuffer indexBuffer = graphicsDevice.CreateIndexBuffer(indices.ToArray(), false);

            // Set up rendering state for transparent billboards
            // Enable alpha blending for transparency
            IBlendState blendState = graphicsDevice.CreateBlendState();
            blendState.BlendEnable = true;
            blendState.ColorSourceBlend = Blend.SourceAlpha;
            blendState.ColorDestinationBlend = Blend.InverseSourceAlpha;
            blendState.AlphaSourceBlend = Blend.SourceAlpha;
            blendState.AlphaDestinationBlend = Blend.InverseSourceAlpha;
            graphicsDevice.SetBlendState(blendState);

            // Disable depth writing for particles (but keep depth testing)
            IDepthStencilState depthState = graphicsDevice.CreateDepthStencilState();
            depthState.DepthBufferEnable = true;
            depthState.DepthBufferWriteEnable = false; // Don't write depth for transparent particles
            graphicsDevice.SetDepthStencilState(depthState);

            // Disable face culling for billboards (they face camera from both sides)
            IRasterizerState rasterState = graphicsDevice.CreateRasterizerState();
            rasterState.CullMode = CullMode.None;
            graphicsDevice.SetRasterizerState(rasterState);

            // Set up effect matrices (identity world matrix since particle positions are already in world space)
            Matrix4x4 identityWorld = Matrix4x4.Identity;
            basicEffect.World = identityWorld;
            basicEffect.View = viewMatrix;
            basicEffect.Projection = projectionMatrix;
            basicEffect.VertexColorEnabled = true; // Enable vertex colors
            basicEffect.LightingEnabled = false; // Disable lighting for particles
            basicEffect.TextureEnabled = false; // No texture for rain (just colored quads)

            // Apply effect and render
            basicEffect.Apply();
            graphicsDevice.SetVertexBuffer(vertexBuffer);
            graphicsDevice.SetIndexBuffer(indexBuffer);

            // Draw indexed primitives
            int primitiveCount = indices.Count / 3; // 3 indices per triangle
            graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, vertices.Count, 0, primitiveCount);

            // Clean up buffers (they are disposed when no longer needed)
            vertexBuffer?.Dispose();
            indexBuffer?.Dispose();
        }
    }
}


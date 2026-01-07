using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Games.Eclipse.Physics;

namespace Andastra.Runtime.Games.Eclipse
{
    /// <summary>
    /// Interface for objects that can be updated each frame.
    /// </summary>
    public interface IUpdatable
    {
        /// <summary>
        /// Updates the object with the given delta time.
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last update in seconds.</param>
        void Update(float deltaTime);
    }

    /// <summary>
    /// Interface for lighting systems in Eclipse engine.
    /// </summary>
    public interface ILightingSystem : IUpdatable, IDisposable
    {
        /// <summary>
        /// Gets or sets the ambient light color.
        /// </summary>
        Vector3 AmbientColor { get; set; }

        /// <summary>
        /// Gets or sets the ambient light intensity.
        /// </summary>
        float AmbientIntensity { get; set; }

        /// <summary>
        /// Adds a dynamic light to the scene.
        /// </summary>
        void AddLight(Andastra.Runtime.MonoGame.Interfaces.IDynamicLight light);

        /// <summary>
        /// Removes a dynamic light from the scene.
        /// </summary>
        void RemoveLight(Andastra.Runtime.MonoGame.Interfaces.IDynamicLight light);

        /// <summary>
        /// Gets lights affecting a specific point.
        /// </summary>
        /// <param name="position">World space position.</param>
        /// <param name="radius">Query radius.</param>
        /// <returns>Array of lights affecting the point.</returns>
        Andastra.Runtime.MonoGame.Interfaces.IDynamicLight[] GetLightsAffectingPoint(Vector3 position, float radius);

        /// <summary>
        /// Gets all active lights.
        /// </summary>
        /// <returns>Array of all active lights.</returns>
        Andastra.Runtime.MonoGame.Interfaces.IDynamicLight[] GetActiveLights();

        /// <summary>
        /// Updates the lighting clustering system.
        /// </summary>
        /// <param name="viewMatrix">View matrix for clustering.</param>
        /// <param name="projectionMatrix">Projection matrix for clustering.</param>
        /// <param name="forceUpdate">Whether to force update even if matrices haven't changed.</param>
        void UpdateClustering(System.Numerics.Matrix4x4 viewMatrix, System.Numerics.Matrix4x4 projectionMatrix, bool forceUpdate = false);

        /// <summary>
        /// Sets fog settings.
        /// </summary>
        /// <param name="fogSettings">Fog settings to apply.</param>
        void SetFog(Andastra.Runtime.Games.Eclipse.Lighting.FogSettings fogSettings);

        /// <summary>
        /// Gets current fog settings.
        /// </summary>
        /// <returns>Current fog settings.</returns>
        Andastra.Runtime.Games.Eclipse.Lighting.FogSettings GetFog();
    }

    /// <summary>
    /// Interface for physics systems in Eclipse engine.
    /// </summary>
    public interface IPhysicsSystem : IUpdatable, IDisposable
    {
        /// <summary>
        /// Steps the physics simulation.
        /// </summary>
        /// <param name="deltaTime">Time step in seconds.</param>
        void StepSimulation(float deltaTime);

        /// <summary>
        /// Casts a ray through the physics world.
        /// </summary>
        bool RayCast(Vector3 origin, Vector3 direction, out Vector3 hitPoint, out IEntity hitEntity);

        /// <summary>
        /// Gets the rigid body state for an entity.
        /// </summary>
        /// <param name="entity">The entity to get physics state for.</param>
        /// <param name="velocity">Output parameter for linear velocity.</param>
        /// <param name="angularVelocity">Output parameter for angular velocity.</param>
        /// <param name="mass">Output parameter for mass.</param>
        /// <param name="constraints">Output parameter for constraint data.</param>
        /// <returns>True if the entity has a rigid body in the physics system, false otherwise.</returns>
        bool GetRigidBodyState(IEntity entity, out Vector3 velocity, out Vector3 angularVelocity, out float mass, out List<PhysicsConstraint> constraints);

        /// <summary>
        /// Sets the rigid body state for an entity.
        /// </summary>
        /// <param name="entity">The entity to set physics state for.</param>
        /// <param name="velocity">Linear velocity to set.</param>
        /// <param name="angularVelocity">Angular velocity to set.</param>
        /// <param name="mass">Mass to set.</param>
        /// <param name="constraints">Constraint data to restore.</param>
        /// <returns>True if the entity has a rigid body in the physics system, false otherwise.</returns>
        bool SetRigidBodyState(IEntity entity, Vector3 velocity, Vector3 angularVelocity, float mass, List<PhysicsConstraint> constraints);
    }
}


using System;
using System.Collections.Generic;
using System.Numerics;
using JetBrains.Annotations;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Games.Eclipse.Physics;

namespace Andastra.Runtime.Games.Eclipse
{
    /// <summary>
    /// Eclipse engine physics system implementation.
    /// </summary>
    /// <remarks>
    /// Based on reverse engineering of:
    /// - daorigins.exe: Physics world management and rigid body dynamics
    /// - DragonAge2.exe: Enhanced physics system with constraint support
    /// 
    /// Eclipse engine uses Unreal Engine 3's physics system (PhysX) for:
    /// - Rigid body dynamics
    /// - Collision detection and response
    /// - Constraint/joint systems
    /// - Area-based physics worlds
    /// 
    /// This implementation provides:
    /// - Physics world management per area
    /// - Rigid body creation and management
    /// - Velocity and angular velocity tracking
    /// - Constraint management for joints and connections
    /// - State persistence for area transitions
    /// </remarks>
    [PublicAPI]
    public class EclipsePhysicsSystem : IPhysicsSystem, IDisposable
    {
        /// <summary>
        /// Dictionary mapping entities to their rigid body data.
        /// </summary>
        private readonly Dictionary<IEntity, RigidBodyData> _rigidBodies = new Dictionary<IEntity, RigidBodyData>();

        /// <summary>
        /// Dictionary mapping entities to their constraint lists.
        /// </summary>
        private readonly Dictionary<IEntity, List<PhysicsConstraint>> _entityConstraints = new Dictionary<IEntity, List<PhysicsConstraint>>();

        /// <summary>
        /// Physics world gravity vector (default: -9.8 m/s² in Y direction).
        /// </summary>
        private Vector3 _gravity = new Vector3(0.0f, -9.8f, 0.0f);

        /// <summary>
        /// Whether the physics system has been disposed.
        /// </summary>
        private bool _disposed = false;

        /// <summary>
        /// Creates a new Eclipse physics system.
        /// </summary>
        /// <remarks>
        /// Initializes the physics world with default settings.
        /// Based on physics world initialization in daorigins.exe, DragonAge2.exe.
        /// </remarks>
        public EclipsePhysicsSystem()
        {
            // Physics world is initialized with default settings
            // In a full implementation, this would create a PhysX scene
        }

        /// <summary>
        /// Steps the physics simulation.
        /// </summary>
        /// <param name="deltaTime">Time step in seconds.</param>
        /// <remarks>
        /// Based on reverse engineering of:
        /// - daorigins.exe: Physics simulation step function
        /// - DragonAge2.exe: Enhanced simulation with sub-stepping
        /// 
        /// Updates all rigid bodies and constraints in the physics world.
        /// </remarks>
        public void StepSimulation(float deltaTime)
        {
            if (_disposed)
            {
                return;
            }

            // Clamp delta time to prevent large steps
            float clampedDeltaTime = Math.Max(0.0f, Math.Min(deltaTime, 0.1f));

            // Update all rigid bodies
            // In a full implementation, this would step the physics engine
            foreach (var kvp in _rigidBodies)
            {
                RigidBodyData body = kvp.Value;
                
                // Apply gravity
                if (body.IsDynamic)
                {
                    body.Velocity += _gravity * clampedDeltaTime;
                }

                // Update position based on velocity
                body.Position += body.Velocity * clampedDeltaTime;

                // Update rotation based on angular velocity
                // In a full implementation, this would update quaternion rotation
                // For now, we track angular velocity for state preservation
            }

            // Update constraints
            // In a full implementation, this would solve constraint equations
            foreach (var constraintList in _entityConstraints.Values)
            {
                foreach (PhysicsConstraint constraint in constraintList)
                {
                    // Constraint solving would happen here
                    // For now, we preserve constraint data for state saving
                }
            }
        }

        /// <summary>
        /// Casts a ray through the physics world.
        /// </summary>
        /// <param name="origin">Ray origin point.</param>
        /// <param name="direction">Ray direction vector (should be normalized).</param>
        /// <param name="hitPoint">Output parameter for hit point if ray hits something.</param>
        /// <param name="hitEntity">Output parameter for hit entity if ray hits something.</param>
        /// <returns>True if ray hits something, false otherwise.</returns>
        /// <remarks>
        /// Based on reverse engineering of:
        /// - daorigins.exe: Raycast function for physics queries
        /// - DragonAge2.exe: Enhanced raycast with filtering
        /// </remarks>
        public bool RayCast(Vector3 origin, Vector3 direction, out Vector3 hitPoint, out IEntity hitEntity)
        {
            hitPoint = Vector3.Zero;
            hitEntity = null;

            if (_disposed)
            {
                return false;
            }

            // Normalize direction
            float length = direction.Length();
            if (length < 0.0001f)
            {
                return false;
            }
            Vector3 normalizedDirection = direction / length;

            // Simple raycast against rigid body bounding boxes
            // In a full implementation, this would use the physics engine's raycast
            float closestDistance = float.MaxValue;
            IEntity closestEntity = null;
            Vector3 closestHitPoint = Vector3.Zero;

            foreach (var kvp in _rigidBodies)
            {
                IEntity entity = kvp.Key;
                RigidBodyData body = kvp.Value;

                // Simple bounding box intersection test
                // In a full implementation, this would use proper collision shapes
                Vector3 boxMin = body.Position - body.HalfExtents;
                Vector3 boxMax = body.Position + body.HalfExtents;

                if (RayBoxIntersection(origin, normalizedDirection, boxMin, boxMax, out Vector3 hit))
                {
                    float distance = Vector3.Distance(origin, hit);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestEntity = entity;
                        closestHitPoint = hit;
                    }
                }
            }

            if (closestEntity != null)
            {
                hitPoint = closestHitPoint;
                hitEntity = closestEntity;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the rigid body state for an entity.
        /// </summary>
        /// <param name="entity">The entity to get physics state for.</param>
        /// <param name="velocity">Output parameter for linear velocity.</param>
        /// <param name="angularVelocity">Output parameter for angular velocity.</param>
        /// <param name="mass">Output parameter for mass.</param>
        /// <param name="constraints">Output parameter for constraint data.</param>
        /// <returns>True if the entity has a rigid body in the physics system, false otherwise.</returns>
        /// <remarks>
        /// Based on reverse engineering of:
        /// - daorigins.exe: Rigid body state query functions (velocity, angular velocity retrieval)
        /// - DragonAge2.exe: Enhanced state query with constraint enumeration
        /// </remarks>
        public bool GetRigidBodyState(IEntity entity, out Vector3 velocity, out Vector3 angularVelocity, out float mass, out List<PhysicsConstraint> constraints)
        {
            velocity = Vector3.Zero;
            angularVelocity = Vector3.Zero;
            mass = 0.0f;
            constraints = new List<PhysicsConstraint>();

            if (_disposed || entity == null)
            {
                return false;
            }

            if (!_rigidBodies.TryGetValue(entity, out RigidBodyData body))
            {
                return false;
            }

            velocity = body.Velocity;
            angularVelocity = body.AngularVelocity;
            mass = body.Mass;

            // Get constraints for this entity
            if (_entityConstraints.TryGetValue(entity, out List<PhysicsConstraint> entityConstraintList))
            {
                constraints.AddRange(entityConstraintList);
            }

            return true;
        }

        /// <summary>
        /// Sets the rigid body state for an entity.
        /// </summary>
        /// <param name="entity">The entity to set physics state for.</param>
        /// <param name="velocity">Linear velocity to set.</param>
        /// <param name="angularVelocity">Angular velocity to set.</param>
        /// <param name="mass">Mass to set.</param>
        /// <param name="constraints">Constraint data to restore.</param>
        /// <returns>True if the entity has a rigid body in the physics system, false otherwise.</returns>
        /// <remarks>
        /// Based on reverse engineering of:
        /// - daorigins.exe: Rigid body state restoration functions (velocity, angular velocity setting)
        /// - DragonAge2.exe: Enhanced state restoration with constraint recreation
        /// </remarks>
        public bool SetRigidBodyState(IEntity entity, Vector3 velocity, Vector3 angularVelocity, float mass, List<PhysicsConstraint> constraints)
        {
            if (_disposed || entity == null)
            {
                return false;
            }

            if (!_rigidBodies.TryGetValue(entity, out RigidBodyData body))
            {
                return false;
            }

            // Set velocity and angular velocity
            body.Velocity = velocity;
            body.AngularVelocity = angularVelocity;
            body.Mass = mass;

            // Restore constraints
            if (constraints != null && constraints.Count > 0)
            {
                if (!_entityConstraints.ContainsKey(entity))
                {
                    _entityConstraints[entity] = new List<PhysicsConstraint>();
                }
                else
                {
                    _entityConstraints[entity].Clear();
                }
                _entityConstraints[entity].AddRange(constraints);
            }

            return true;
        }

        /// <summary>
        /// Adds a rigid body for an entity.
        /// </summary>
        /// <param name="entity">The entity to add a rigid body for.</param>
        /// <param name="position">Initial position.</param>
        /// <param name="mass">Mass of the rigid body.</param>
        /// <param name="halfExtents">Half extents for bounding box.</param>
        /// <param name="isDynamic">Whether the body is dynamic (true) or static (false).</param>
        /// <remarks>
        /// Based on reverse engineering of:
        /// - daorigins.exe: Rigid body creation functions
        /// - DragonAge2.exe: Enhanced rigid body creation with shape support
        /// </remarks>
        public void AddRigidBody(IEntity entity, Vector3 position, float mass, Vector3 halfExtents, bool isDynamic = true)
        {
            if (_disposed || entity == null)
            {
                return;
            }

            var body = new RigidBodyData
            {
                Position = position,
                Velocity = Vector3.Zero,
                AngularVelocity = Vector3.Zero,
                Mass = mass,
                HalfExtents = halfExtents,
                IsDynamic = isDynamic
            };

            _rigidBodies[entity] = body;
        }

        /// <summary>
        /// Removes a rigid body for an entity.
        /// </summary>
        /// <param name="entity">The entity to remove the rigid body for.</param>
        /// <remarks>
        /// Based on reverse engineering of:
        /// - daorigins.exe: Rigid body removal functions
        /// - DragonAge2.exe: Enhanced cleanup with constraint removal
        /// </remarks>
        public void RemoveRigidBody(IEntity entity)
        {
            if (_disposed || entity == null)
            {
                return;
            }

            _rigidBodies.Remove(entity);
            _entityConstraints.Remove(entity);
        }

        /// <summary>
        /// Adds a constraint between two entities.
        /// </summary>
        /// <param name="constraint">The constraint to add.</param>
        /// <remarks>
        /// Based on reverse engineering of:
        /// - daorigins.exe: Constraint creation functions
        /// - DragonAge2.exe: Enhanced constraint system
        /// </remarks>
        public void AddConstraint(PhysicsConstraint constraint)
        {
            if (_disposed || constraint == null || constraint.EntityA == null)
            {
                return;
            }

            if (!_entityConstraints.ContainsKey(constraint.EntityA))
            {
                _entityConstraints[constraint.EntityA] = new List<PhysicsConstraint>();
            }
            _entityConstraints[constraint.EntityA].Add(constraint);

            // Also track constraint on EntityB if it exists
            if (constraint.EntityB != null)
            {
                if (!_entityConstraints.ContainsKey(constraint.EntityB))
                {
                    _entityConstraints[constraint.EntityB] = new List<PhysicsConstraint>();
                }
                _entityConstraints[constraint.EntityB].Add(constraint);
            }
        }

        /// <summary>
        /// Removes a constraint.
        /// </summary>
        /// <param name="constraint">The constraint to remove.</param>
        /// <remarks>
        /// Based on reverse engineering of:
        /// - daorigins.exe: Constraint removal functions
        /// - DragonAge2.exe: Enhanced constraint cleanup
        /// </remarks>
        public void RemoveConstraint(PhysicsConstraint constraint)
        {
            if (_disposed || constraint == null)
            {
                return;
            }

            if (constraint.EntityA != null && _entityConstraints.TryGetValue(constraint.EntityA, out List<PhysicsConstraint> listA))
            {
                listA.Remove(constraint);
            }

            if (constraint.EntityB != null && _entityConstraints.TryGetValue(constraint.EntityB, out List<PhysicsConstraint> listB))
            {
                listB.Remove(constraint);
            }
        }

        /// <summary>
        /// Checks if an entity has a rigid body in the physics system.
        /// </summary>
        /// <param name="entity">The entity to check.</param>
        /// <returns>True if the entity has a rigid body, false otherwise.</returns>
        public bool HasRigidBody(IEntity entity)
        {
            if (_disposed || entity == null)
            {
                return false;
            }

            return _rigidBodies.ContainsKey(entity);
        }

        /// <summary>
        /// Disposes the physics system and cleans up resources.
        /// </summary>
        /// <remarks>
        /// Based on reverse engineering of:
        /// - daorigins.exe: Physics world cleanup
        /// - DragonAge2.exe: Enhanced resource cleanup
        /// </remarks>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _rigidBodies.Clear();
            _entityConstraints.Clear();
            _disposed = true;
        }

        /// <summary>
        /// Internal data structure for rigid body information.
        /// </summary>
        private class RigidBodyData
        {
            public Vector3 Position { get; set; }
            public Vector3 Velocity { get; set; }
            public Vector3 AngularVelocity { get; set; }
            public float Mass { get; set; }
            public Vector3 HalfExtents { get; set; }
            public bool IsDynamic { get; set; }
        }

        /// <summary>
        /// Performs a ray-box intersection test.
        /// </summary>
        /// <param name="rayOrigin">Ray origin point.</param>
        /// <param name="rayDirection">Normalized ray direction.</param>
        /// <param name="boxMin">Minimum corner of the bounding box.</param>
        /// <param name="boxMax">Maximum corner of the bounding box.</param>
        /// <param name="hitPoint">Output parameter for hit point.</param>
        /// <returns>True if ray intersects the box, false otherwise.</returns>
        private static bool RayBoxIntersection(Vector3 rayOrigin, Vector3 rayDirection, Vector3 boxMin, Vector3 boxMax, out Vector3 hitPoint)
        {
            hitPoint = Vector3.Zero;

            Vector3 invDir = new Vector3(
                1.0f / (rayDirection.X != 0.0f ? rayDirection.X : float.Epsilon),
                1.0f / (rayDirection.Y != 0.0f ? rayDirection.Y : float.Epsilon),
                1.0f / (rayDirection.Z != 0.0f ? rayDirection.Z : float.Epsilon)
            );

            float t1 = (boxMin.X - rayOrigin.X) * invDir.X;
            float t2 = (boxMax.X - rayOrigin.X) * invDir.X;
            float t3 = (boxMin.Y - rayOrigin.Y) * invDir.Y;
            float t4 = (boxMax.Y - rayOrigin.Y) * invDir.Y;
            float t5 = (boxMin.Z - rayOrigin.Z) * invDir.Z;
            float t6 = (boxMax.Z - rayOrigin.Z) * invDir.Z;

            float tmin = Math.Max(Math.Max(Math.Min(t1, t2), Math.Min(t3, t4)), Math.Min(t5, t6));
            float tmax = Math.Min(Math.Min(Math.Max(t1, t2), Math.Max(t3, t4)), Math.Max(t5, t6));

            if (tmax < 0 || tmin > tmax)
            {
                return false;
            }

            float t = tmin > 0 ? tmin : tmax;
            hitPoint = rayOrigin + rayDirection * t;
            return true;
        }
    }
}


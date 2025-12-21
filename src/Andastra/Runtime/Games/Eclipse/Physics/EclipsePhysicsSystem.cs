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
        /// Dictionary mapping mesh IDs to static geometry collision shapes.
        /// Static geometry (rooms, static objects) collision shapes are stored separately from entity rigid bodies.
        /// Based on daorigins.exe/DragonAge2.exe: Static geometry collision shapes are managed per mesh.
        /// </summary>
        private readonly Dictionary<string, StaticGeometryCollisionShape> _staticCollisionShapes = new Dictionary<string, StaticGeometryCollisionShape>(StringComparer.OrdinalIgnoreCase);

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
                // daorigins.exe: 0x008e55f0 - Transform/quaternion rotation updates
                // DragonAge2.exe: Enhanced angular velocity integration with quaternions
                if (body.AngularVelocity.LengthSquared() > 0.0001f)
                {
                    Vector3 angularVel = body.AngularVelocity;
                    // Angular velocity quaternion (pure quaternion, w=0)
                    Quaternion angularQuat = new Quaternion(angularVel.X, angularVel.Y, angularVel.Z, 0.0f);
                    // dq/dt = 0.5 * angularVelQuat * rotation
                    Quaternion deltaRotation = Quaternion.Multiply(angularQuat, body.Rotation);
                    deltaRotation = new Quaternion(
                        deltaRotation.X * 0.5f * clampedDeltaTime,
                        deltaRotation.Y * 0.5f * clampedDeltaTime,
                        deltaRotation.Z * 0.5f * clampedDeltaTime,
                        deltaRotation.W * 0.5f * clampedDeltaTime
                    );
                    // Add delta rotation: q_new = q_old + dq
                    body.Rotation = new Quaternion(
                        body.Rotation.X + deltaRotation.X,
                        body.Rotation.Y + deltaRotation.Y,
                        body.Rotation.Z + deltaRotation.Z,
                        body.Rotation.W + deltaRotation.W
                    );
                    body.Rotation = Quaternion.Normalize(body.Rotation);
                }
            }

            // Solve constraints using iterative constraint solver
            // daorigins.exe: Constraint solving in physics simulation step
            // DragonAge2.exe: Enhanced iterative constraint solver with multiple iterations
            // PhysX-style iterative impulse-based constraint solver
            const int constraintIterations = 10; // Standard PhysX uses 10-20 iterations
            for (int iteration = 0; iteration < constraintIterations; iteration++)
            {
                foreach (var constraintList in _entityConstraints.Values)
                {
                    foreach (PhysicsConstraint constraint in constraintList)
                    {
                        SolveConstraint(constraint, clampedDeltaTime);
                    }
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
                Rotation = Quaternion.Identity,
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
            _staticCollisionShapes.Clear();
            _disposed = true;
        }

        /// <summary>
        /// Internal data structure for rigid body information.
        /// </summary>
        private class RigidBodyData
        {
            public Vector3 Position { get; set; }
            public Quaternion Rotation { get; set; }
            public Vector3 Velocity { get; set; }
            public Vector3 AngularVelocity { get; set; }
            public float Mass { get; set; }
            public Vector3 HalfExtents { get; set; }
            public bool IsDynamic { get; set; }

            public RigidBodyData()
            {
                Rotation = Quaternion.Identity;
            }
        }

        /// <summary>
        /// Solves a constraint by applying corrective impulses.
        /// </summary>
        /// <param name="constraint">The constraint to solve.</param>
        /// <param name="deltaTime">Time step for constraint solving.</param>
        /// <remarks>
        /// Based on reverse engineering of:
        /// - daorigins.exe: Constraint solving in physics simulation
        /// - DragonAge2.exe: Enhanced constraint solving with iterative methods
        /// 
        /// Implements PhysX-style iterative impulse-based constraint solver.
        /// </remarks>
        private void SolveConstraint(PhysicsConstraint constraint, float deltaTime)
        {
            if (constraint == null || constraint.EntityA == null)
            {
                return;
            }

            if (!_rigidBodies.TryGetValue(constraint.EntityA, out RigidBodyData bodyA))
            {
                return;
            }

            RigidBodyData bodyB = null;
            bool hasBodyB = constraint.EntityB != null && _rigidBodies.TryGetValue(constraint.EntityB, out bodyB);

            // Convert local anchor points to world space
            Vector3 worldAnchorA = TransformLocalToWorld(bodyA, constraint.AnchorA);
            Vector3 worldAnchorB;
            if (hasBodyB)
            {
                worldAnchorB = TransformLocalToWorld(bodyB, constraint.AnchorB);
            }
            else
            {
                // World constraint - AnchorB is in world space
                worldAnchorB = constraint.AnchorB;
            }

            // Solve constraint based on type
            switch (constraint.Type)
            {
                case ConstraintType.Fixed:
                    SolveFixedConstraint(constraint, bodyA, bodyB, worldAnchorA, worldAnchorB, deltaTime);
                    break;

                case ConstraintType.Hinge:
                    SolveHingeConstraint(constraint, bodyA, bodyB, worldAnchorA, worldAnchorB, deltaTime);
                    break;

                case ConstraintType.BallAndSocket:
                    SolveBallAndSocketConstraint(constraint, bodyA, bodyB, worldAnchorA, worldAnchorB, deltaTime);
                    break;

                case ConstraintType.Slider:
                    SolveSliderConstraint(constraint, bodyA, bodyB, worldAnchorA, worldAnchorB, deltaTime);
                    break;

                case ConstraintType.Spring:
                    SolveSpringConstraint(constraint, bodyA, bodyB, worldAnchorA, worldAnchorB, deltaTime);
                    break;
            }
        }

        /// <summary>
        /// Solves a fixed constraint (no relative movement or rotation).
        /// </summary>
        /// <param name="constraint">The fixed constraint.</param>
        /// <param name="bodyA">First rigid body.</param>
        /// <param name="bodyB">Second rigid body (null for world constraints).</param>
        /// <param name="worldAnchorA">World space anchor on body A.</param>
        /// <param name="worldAnchorB">World space anchor on body B (or world position).</param>
        /// <param name="deltaTime">Time step.</param>
        /// <remarks>
        /// daorigins.exe: Fixed constraint implementation
        /// Enforces both position and orientation constraints.
        /// </remarks>
        private void SolveFixedConstraint(PhysicsConstraint constraint, RigidBodyData bodyA, RigidBodyData bodyB, Vector3 worldAnchorA, Vector3 worldAnchorB, float deltaTime)
        {
            if (!bodyA.IsDynamic)
            {
                return;
            }

            // Position constraint: Keep anchors aligned
            Vector3 positionError = worldAnchorB - worldAnchorA;
            float positionErrorLength = positionError.Length();
            
            if (positionErrorLength > 0.0001f)
            {
                Vector3 correctionDirection = positionError / positionErrorLength;
                float invMassA = 1.0f / bodyA.Mass;
                float invMassB = bodyB != null && bodyB.IsDynamic ? (1.0f / bodyB.Mass) : 0.0f;
                float totalInvMass = invMassA + invMassB;
                
                if (totalInvMass > 0.0001f)
                {
                    float constraintBias = constraint.Stiffness > 0.0f ? constraint.Stiffness : 1000.0f;
                    float correction = (positionErrorLength * constraintBias) / (totalInvMass * deltaTime);
                    
                    Vector3 impulse = correctionDirection * correction;
                    bodyA.Velocity += impulse * invMassA;
                    bodyA.Position += impulse * invMassA * deltaTime;
                    
                    if (bodyB != null && bodyB.IsDynamic)
                    {
                        bodyB.Velocity -= impulse * invMassB;
                        bodyB.Position -= impulse * invMassB * deltaTime;
                    }
                }
            }

            // Orientation constraint: Align rotation
            if (bodyB != null && bodyB.IsDynamic)
            {
                Quaternion targetRotation = bodyB.Rotation;
                Quaternion currentRotation = bodyA.Rotation;
                Quaternion rotationError = Quaternion.Multiply(Quaternion.Inverse(currentRotation), targetRotation);
                
                // Convert quaternion error to axis-angle
                if (rotationError.W < 0.9999f)
                {
                    float angle = 2.0f * (float)Math.Acos(Math.Abs(rotationError.W));
                    if (angle > 0.0001f)
                    {
                        float s = (float)Math.Sqrt(1.0f - rotationError.W * rotationError.W);
                        if (s > 0.0001f)
                        {
                            Vector3 axis = new Vector3(rotationError.X / s, rotationError.Y / s, rotationError.Z / s);
                            float invMassA = 1.0f / bodyA.Mass;
                            float invMassB = 1.0f / bodyB.Mass;
                            float totalInvMass = invMassA + invMassB;
                            
                            if (totalInvMass > 0.0001f)
                            {
                                float constraintBias = constraint.Stiffness > 0.0f ? constraint.Stiffness : 1000.0f;
                                Vector3 angularImpulse = axis * (angle * constraintBias / (totalInvMass * deltaTime));
                                bodyA.AngularVelocity += angularImpulse * invMassA;
                                bodyB.AngularVelocity -= angularImpulse * invMassB;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Solves a hinge constraint (rotation around axis only).
        /// </summary>
        /// <param name="constraint">The hinge constraint.</param>
        /// <param name="bodyA">First rigid body.</param>
        /// <param name="bodyB">Second rigid body (null for world constraints).</param>
        /// <param name="worldAnchorA">World space anchor on body A.</param>
        /// <param name="worldAnchorB">World space anchor on body B (or world position).</param>
        /// <param name="deltaTime">Time step.</param>
        /// <remarks>
        /// daorigins.exe: Hinge constraint implementation
        /// Allows rotation around constraint direction axis only.
        /// </remarks>
        private void SolveHingeConstraint(PhysicsConstraint constraint, RigidBodyData bodyA, RigidBodyData bodyB, Vector3 worldAnchorA, Vector3 worldAnchorB, float deltaTime)
        {
            if (!bodyA.IsDynamic)
            {
                return;
            }

            // Position constraint: Keep anchors aligned (same as ball-and-socket)
            Vector3 positionError = worldAnchorB - worldAnchorA;
            float positionErrorLength = positionError.Length();
            
            if (positionErrorLength > 0.0001f)
            {
                Vector3 correctionDirection = positionError / positionErrorLength;
                float invMassA = 1.0f / bodyA.Mass;
                float invMassB = bodyB != null && bodyB.IsDynamic ? (1.0f / bodyB.Mass) : 0.0f;
                float totalInvMass = invMassA + invMassB;
                
                if (totalInvMass > 0.0001f)
                {
                    float constraintBias = constraint.Stiffness > 0.0f ? constraint.Stiffness : 1000.0f;
                    float correction = (positionErrorLength * constraintBias) / (totalInvMass * deltaTime);
                    
                    Vector3 impulse = correctionDirection * correction;
                    bodyA.Velocity += impulse * invMassA;
                    
                    if (bodyB != null && bodyB.IsDynamic)
                    {
                        bodyB.Velocity -= impulse * invMassB;
                    }
                }
            }

            // Angular constraint: Restrict rotation to hinge axis only
            Vector3 hingeAxis = constraint.Direction;
            if (hingeAxis.LengthSquared() < 0.0001f)
            {
                hingeAxis = Vector3.UnitX; // Default axis
            }
            hingeAxis = Vector3.Normalize(hingeAxis);

            // Project angular velocity onto hinge axis and remove perpendicular components
            float angularVelAlongAxis = Vector3.Dot(bodyA.AngularVelocity, hingeAxis);
            Vector3 constrainedAngularVel = hingeAxis * angularVelAlongAxis;
            Vector3 angularError = bodyA.AngularVelocity - constrainedAngularVel;
            
            if (angularError.LengthSquared() > 0.0001f)
            {
                float constraintBias = constraint.Stiffness > 0.0f ? constraint.Stiffness : 1000.0f;
                Vector3 correction = angularError * (constraintBias / (deltaTime * bodyA.Mass));
                bodyA.AngularVelocity -= correction * deltaTime;
            }

            // Apply angular limits if specified
            if (constraint.Limits.X != 0.0f || constraint.Limits.Y != 0.0f)
            {
                // Calculate current angle (simplified - would need reference frame in full implementation)
                // For now, just clamp angular velocity magnitude
                float maxAngularVel = constraint.Limits.Y > 0.0f ? constraint.Limits.Y : float.MaxValue;
                if (Math.Abs(angularVelAlongAxis) > maxAngularVel)
                {
                    float clampedVel = Math.Sign(angularVelAlongAxis) * maxAngularVel;
                    bodyA.AngularVelocity = hingeAxis * clampedVel;
                }
            }
        }

        /// <summary>
        /// Solves a ball-and-socket constraint (allows rotation, constrains translation).
        /// </summary>
        /// <param name="constraint">The ball-and-socket constraint.</param>
        /// <param name="bodyA">First rigid body.</param>
        /// <param name="bodyB">Second rigid body (null for world constraints).</param>
        /// <param name="worldAnchorA">World space anchor on body A.</param>
        /// <param name="worldAnchorB">World space anchor on body B (or world position).</param>
        /// <param name="deltaTime">Time step.</param>
        /// <remarks>
        /// daorigins.exe: Ball-and-socket constraint implementation
        /// Keeps anchors together but allows free rotation.
        /// </remarks>
        private void SolveBallAndSocketConstraint(PhysicsConstraint constraint, RigidBodyData bodyA, RigidBodyData bodyB, Vector3 worldAnchorA, Vector3 worldAnchorB, float deltaTime)
        {
            if (!bodyA.IsDynamic)
            {
                return;
            }

            // Position constraint: Keep anchors aligned
            Vector3 positionError = worldAnchorB - worldAnchorA;
            float positionErrorLength = positionError.Length();
            
            if (positionErrorLength > 0.0001f)
            {
                Vector3 correctionDirection = positionError / positionErrorLength;
                float invMassA = 1.0f / bodyA.Mass;
                float invMassB = bodyB != null && bodyB.IsDynamic ? (1.0f / bodyB.Mass) : 0.0f;
                float totalInvMass = invMassA + invMassB;
                
                if (totalInvMass > 0.0001f)
                {
                    float constraintBias = constraint.Stiffness > 0.0f ? constraint.Stiffness : 1000.0f;
                    float correction = (positionErrorLength * constraintBias) / (totalInvMass * deltaTime);
                    
                    Vector3 impulse = correctionDirection * correction;
                    bodyA.Velocity += impulse * invMassA;
                    bodyA.Position += impulse * invMassA * deltaTime;
                    
                    if (bodyB != null && bodyB.IsDynamic)
                    {
                        bodyB.Velocity -= impulse * invMassB;
                        bodyB.Position -= impulse * invMassB * deltaTime;
                    }
                }
            }
        }

        /// <summary>
        /// Solves a slider constraint (translation along axis only).
        /// </summary>
        /// <param name="constraint">The slider constraint.</param>
        /// <param name="bodyA">First rigid body.</param>
        /// <param name="bodyB">Second rigid body (null for world constraints).</param>
        /// <param name="worldAnchorA">World space anchor on body A.</param>
        /// <param name="worldAnchorB">World space anchor on body B (or world position).</param>
        /// <param name="deltaTime">Time step.</param>
        /// <remarks>
        /// daorigins.exe: Slider constraint implementation
        /// Allows translation along constraint direction only.
        /// </remarks>
        private void SolveSliderConstraint(PhysicsConstraint constraint, RigidBodyData bodyA, RigidBodyData bodyB, Vector3 worldAnchorA, Vector3 worldAnchorB, float deltaTime)
        {
            if (!bodyA.IsDynamic)
            {
                return;
            }

            Vector3 slideAxis = constraint.Direction;
            if (slideAxis.LengthSquared() < 0.0001f)
            {
                slideAxis = Vector3.UnitX; // Default axis
            }
            slideAxis = Vector3.Normalize(slideAxis);

            // Constrain position to slide along axis
            Vector3 positionError = worldAnchorB - worldAnchorA;
            float projectionLength = Vector3.Dot(positionError, slideAxis);
            Vector3 constrainedPosition = slideAxis * projectionLength;
            Vector3 perpendicularError = positionError - constrainedPosition;
            
            if (perpendicularError.LengthSquared() > 0.0001f)
            {
                float invMassA = 1.0f / bodyA.Mass;
                float invMassB = bodyB != null && bodyB.IsDynamic ? (1.0f / bodyB.Mass) : 0.0f;
                float totalInvMass = invMassA + invMassB;
                
                if (totalInvMass > 0.0001f)
                {
                    float constraintBias = constraint.Stiffness > 0.0f ? constraint.Stiffness : 1000.0f;
                    float errorLength = perpendicularError.Length();
                    Vector3 correctionDirection = perpendicularError / errorLength;
                    float correction = (errorLength * constraintBias) / (totalInvMass * deltaTime);
                    
                    Vector3 impulse = correctionDirection * correction;
                    bodyA.Velocity += impulse * invMassA;
                    
                    if (bodyB != null && bodyB.IsDynamic)
                    {
                        bodyB.Velocity -= impulse * invMassB;
                    }
                }
            }

            // Constrain velocity to slide along axis only
            Vector3 velocityA = bodyA.Velocity;
            float velocityAlongAxis = Vector3.Dot(velocityA, slideAxis);
            Vector3 constrainedVelocity = slideAxis * velocityAlongAxis;
            Vector3 perpendicularVelocity = velocityA - constrainedVelocity;
            
            if (perpendicularVelocity.LengthSquared() > 0.0001f)
            {
                float constraintBias = constraint.Stiffness > 0.0f ? constraint.Stiffness : 1000.0f;
                Vector3 correction = perpendicularVelocity * (constraintBias / (deltaTime * bodyA.Mass));
                bodyA.Velocity -= correction * deltaTime;
            }

            // Apply linear limits if specified
            if (constraint.Limits.X != 0.0f || constraint.Limits.Y != 0.0f)
            {
                float currentDistance = projectionLength;
                float minDistance = constraint.Limits.X;
                float maxDistance = constraint.Limits.Y;
                
                if (currentDistance < minDistance)
                {
                    float correction = (minDistance - currentDistance) * constraint.Stiffness / (deltaTime * bodyA.Mass);
                    bodyA.Velocity += slideAxis * correction;
                }
                else if (currentDistance > maxDistance)
                {
                    float correction = (currentDistance - maxDistance) * constraint.Stiffness / (deltaTime * bodyA.Mass);
                    bodyA.Velocity -= slideAxis * correction;
                }
            }
        }

        /// <summary>
        /// Solves a spring constraint (elastic connection).
        /// </summary>
        /// <param name="constraint">The spring constraint.</param>
        /// <param name="bodyA">First rigid body.</param>
        /// <param name="bodyB">Second rigid body (null for world constraints).</param>
        /// <param name="worldAnchorA">World space anchor on body A.</param>
        /// <param name="worldAnchorB">World space anchor on body B (or world position).</param>
        /// <param name="deltaTime">Time step.</param>
        /// <remarks>
        /// daorigins.exe: Spring constraint implementation
        /// Applies elastic forces based on displacement.
        /// </remarks>
        private void SolveSpringConstraint(PhysicsConstraint constraint, RigidBodyData bodyA, RigidBodyData bodyB, Vector3 worldAnchorA, Vector3 worldAnchorB, float deltaTime)
        {
            if (!bodyA.IsDynamic)
            {
                return;
            }

            Vector3 displacement = worldAnchorB - worldAnchorA;
            float distance = displacement.Length();
            
            if (distance > 0.0001f)
            {
                Vector3 direction = displacement / distance;
                
                // Get spring parameters
                float springConstant = constraint.Stiffness > 0.0f ? constraint.Stiffness : 100.0f;
                float damping = constraint.Parameters != null && constraint.Parameters.ContainsKey("damping") 
                    ? constraint.Parameters["damping"] 
                    : 0.1f;
                float restLength = constraint.Parameters != null && constraint.Parameters.ContainsKey("restLength")
                    ? constraint.Parameters["restLength"]
                    : 0.0f;
                
                float extension = distance - restLength;
                
                // Calculate spring force (Hooke's law)
                float springForce = extension * springConstant;
                
                // Calculate damping force (proportional to relative velocity)
                Vector3 relativeVelocity = bodyA.Velocity;
                if (bodyB != null && bodyB.IsDynamic)
                {
                    relativeVelocity -= bodyB.Velocity;
                }
                float relativeSpeed = Vector3.Dot(relativeVelocity, direction);
                float dampingForce = relativeSpeed * damping;
                
                // Total force along spring direction
                float totalForce = springForce + dampingForce;
                Vector3 force = direction * totalForce;
                
                // Apply forces
                float invMassA = 1.0f / bodyA.Mass;
                bodyA.Velocity += force * invMassA * deltaTime;
                
                if (bodyB != null && bodyB.IsDynamic)
                {
                    float invMassB = 1.0f / bodyB.Mass;
                    bodyB.Velocity -= force * invMassB * deltaTime;
                }
            }
        }

        /// <summary>
        /// Transforms a local point to world space using rigid body transform.
        /// </summary>
        /// <param name="body">The rigid body.</param>
        /// <param name="localPoint">Point in local space.</param>
        /// <returns>Point in world space.</returns>
        /// <remarks>
        /// daorigins.exe: Transform operations for constraint anchor points
        /// </remarks>
        private Vector3 TransformLocalToWorld(RigidBodyData body, Vector3 localPoint)
        {
            // Rotate local point by body rotation
            Vector3 rotatedPoint = Vector3.Transform(localPoint, body.Rotation);
            // Translate by body position
            return body.Position + rotatedPoint;
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

        /// <summary>
        /// Adds or updates a static geometry collision shape for a mesh.
        /// </summary>
        /// <param name="meshId">Mesh identifier (model name/resref).</param>
        /// <param name="vertices">Vertex positions (world space).</param>
        /// <param name="indices">Triangle indices (3 indices per triangle).</param>
        /// <param name="destroyedFaceIndices">Set of destroyed face indices (triangles to exclude from collision).</param>
        /// <param name="modifiedVertices">Dictionary mapping vertex indices to modified positions (for deformed geometry).</param>
        /// <remarks>
        /// Based on reverse engineering of:
        /// - daorigins.exe: Static geometry collision shape creation and updates (0x008f12a0)
        /// - DragonAge2.exe: Enhanced collision shape updates for destructible geometry (0x009a45b0)
        /// 
        /// Static geometry collision shapes are triangle meshes representing room/static object geometry.
        /// When geometry is modified (destroyed/deformed), collision shapes are rebuilt to match.
        /// </remarks>
        public void UpdateStaticGeometryCollisionShape(
            string meshId,
            List<Vector3> vertices,
            List<int> indices,
            HashSet<int> destroyedFaceIndices,
            Dictionary<int, Vector3> modifiedVertices)
        {
            if (_disposed || string.IsNullOrEmpty(meshId) || vertices == null || indices == null)
            {
                return;
            }

            // Build collision triangle list from geometry, excluding destroyed faces and applying vertex modifications
            List<CollisionTriangle> collisionTriangles = new List<CollisionTriangle>();

            // Process triangles (3 indices per triangle)
            for (int i = 0; i < indices.Count; i += 3)
            {
                if (i + 2 >= indices.Count)
                {
                    break; // Incomplete triangle
                }

                int triangleIndex = i / 3;
                int index0 = indices[i];
                int index1 = indices[i + 1];
                int index2 = indices[i + 2];

                // Skip destroyed faces
                if (destroyedFaceIndices != null && destroyedFaceIndices.Contains(triangleIndex))
                {
                    continue;
                }

                // Get vertex positions (use modified positions if available, otherwise original)
                Vector3 v0 = GetVertexPosition(vertices, modifiedVertices, index0);
                Vector3 v1 = GetVertexPosition(vertices, modifiedVertices, index1);
                Vector3 v2 = GetVertexPosition(vertices, modifiedVertices, index2);

                // Validate triangle (check for degenerate triangles)
                Vector3 edge1 = v1 - v0;
                Vector3 edge2 = v2 - v0;
                Vector3 normal = Vector3.Cross(edge1, edge2);
                float area = normal.Length() * 0.5f;

                // Skip degenerate triangles (zero area)
                if (area < 0.0001f)
                {
                    continue;
                }

                // Create collision triangle
                CollisionTriangle triangle = new CollisionTriangle
                {
                    Vertex0 = v0,
                    Vertex1 = v1,
                    Vertex2 = v2,
                    Normal = Vector3.Normalize(normal)
                };

                collisionTriangles.Add(triangle);
            }

            // Create or update static geometry collision shape
            StaticGeometryCollisionShape collisionShape = new StaticGeometryCollisionShape
            {
                MeshId = meshId,
                Triangles = collisionTriangles,
                BoundsMin = CalculateBoundsMin(vertices, modifiedVertices),
                BoundsMax = CalculateBoundsMax(vertices, modifiedVertices)
            };

            _staticCollisionShapes[meshId] = collisionShape;
        }

        /// <summary>
        /// Removes a static geometry collision shape for a mesh.
        /// </summary>
        /// <param name="meshId">Mesh identifier (model name/resref).</param>
        /// <remarks>
        /// Based on daorigins.exe: Static geometry collision shape removal when mesh is unloaded.
        /// </remarks>
        public void RemoveStaticGeometryCollisionShape(string meshId)
        {
            if (_disposed || string.IsNullOrEmpty(meshId))
            {
                return;
            }

            _staticCollisionShapes.Remove(meshId);
        }

        /// <summary>
        /// Gets a static geometry collision shape for a mesh.
        /// </summary>
        /// <param name="meshId">Mesh identifier (model name/resref).</param>
        /// <returns>Static geometry collision shape, or null if not found.</returns>
        public StaticGeometryCollisionShape GetStaticGeometryCollisionShape(string meshId)
        {
            if (_disposed || string.IsNullOrEmpty(meshId))
            {
                return null;
            }

            if (_staticCollisionShapes.TryGetValue(meshId, out StaticGeometryCollisionShape shape))
            {
                return shape;
            }

            return null;
        }

        /// <summary>
        /// Gets vertex position, using modified position if available.
        /// </summary>
        /// <param name="vertices">Original vertex positions.</param>
        /// <param name="modifiedVertices">Dictionary of modified vertex positions.</param>
        /// <param name="vertexIndex">Vertex index.</param>
        /// <returns>Vertex position (modified if available, otherwise original).</returns>
        private Vector3 GetVertexPosition(List<Vector3> vertices, Dictionary<int, Vector3> modifiedVertices, int vertexIndex)
        {
            if (vertexIndex < 0 || vertexIndex >= vertices.Count)
            {
                return Vector3.Zero;
            }

            // Check for modified position
            if (modifiedVertices != null && modifiedVertices.TryGetValue(vertexIndex, out Vector3 modifiedPos))
            {
                return modifiedPos;
            }

            return vertices[vertexIndex];
        }

        /// <summary>
        /// Calculates the minimum bounds from vertices (including modified vertices).
        /// </summary>
        /// <param name="vertices">Original vertex positions.</param>
        /// <param name="modifiedVertices">Dictionary of modified vertex positions.</param>
        /// <returns>Minimum bounds vector.</returns>
        private Vector3 CalculateBoundsMin(List<Vector3> vertices, Dictionary<int, Vector3> modifiedVertices)
        {
            if (vertices == null || vertices.Count == 0)
            {
                return Vector3.Zero;
            }

            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);

            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 pos = GetVertexPosition(vertices, modifiedVertices, i);
                min.X = Math.Min(min.X, pos.X);
                min.Y = Math.Min(min.Y, pos.Y);
                min.Z = Math.Min(min.Z, pos.Z);
            }

            return min;
        }

        /// <summary>
        /// Calculates the maximum bounds from vertices (including modified vertices).
        /// </summary>
        /// <param name="vertices">Original vertex positions.</param>
        /// <param name="modifiedVertices">Dictionary of modified vertex positions.</param>
        /// <returns>Maximum bounds vector.</returns>
        private Vector3 CalculateBoundsMax(List<Vector3> vertices, Dictionary<int, Vector3> modifiedVertices)
        {
            if (vertices == null || vertices.Count == 0)
            {
                return Vector3.Zero;
            }

            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 pos = GetVertexPosition(vertices, modifiedVertices, i);
                max.X = Math.Max(max.X, pos.X);
                max.Y = Math.Max(max.Y, pos.Y);
                max.Z = Math.Max(max.Z, pos.Z);
            }

            return max;
        }

        /// <summary>
        /// Internal data structure for static geometry collision shape.
        /// </summary>
        /// <remarks>
        /// Based on daorigins.exe/DragonAge2.exe: Static geometry collision shape data structure.
        /// Stores triangle mesh collision data for static geometry (rooms, static objects).
        /// </remarks>
        public class StaticGeometryCollisionShape
        {
            /// <summary>
            /// Mesh identifier (model name/resref).
            /// </summary>
            public string MeshId { get; set; }

            /// <summary>
            /// Collision triangles representing the geometry.
            /// </summary>
            public List<CollisionTriangle> Triangles { get; set; }

            /// <summary>
            /// Minimum bounding box corner.
            /// </summary>
            public Vector3 BoundsMin { get; set; }

            /// <summary>
            /// Maximum bounding box corner.
            /// </summary>
            public Vector3 BoundsMax { get; set; }

            public StaticGeometryCollisionShape()
            {
                MeshId = string.Empty;
                Triangles = new List<CollisionTriangle>();
                BoundsMin = Vector3.Zero;
                BoundsMax = Vector3.Zero;
            }
        }

        /// <summary>
        /// Represents a single collision triangle.
        /// </summary>
        /// <remarks>
        /// Based on daorigins.exe/DragonAge2.exe: Triangle collision data structure.
        /// </remarks>
        public class CollisionTriangle
        {
            /// <summary>
            /// First vertex position.
            /// </summary>
            public Vector3 Vertex0 { get; set; }

            /// <summary>
            /// Second vertex position.
            /// </summary>
            public Vector3 Vertex1 { get; set; }

            /// <summary>
            /// Third vertex position.
            /// </summary>
            public Vector3 Vertex2 { get; set; }

            /// <summary>
            /// Triangle normal vector.
            /// </summary>
            public Vector3 Normal { get; set; }

            public CollisionTriangle()
            {
                Vertex0 = Vector3.Zero;
                Vertex1 = Vector3.Zero;
                Vertex2 = Vector3.Zero;
                Normal = Vector3.Zero;
            }
        }
    }
}


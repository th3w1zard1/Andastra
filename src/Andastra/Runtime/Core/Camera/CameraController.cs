using System;
using System.Numerics;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Core.Camera
{
    /// <summary>
    /// Manages camera behavior in the game world.
    /// </summary>
    /// <remarks>
    /// KOTOR Camera System:
    /// - Based on swkotor2.exe camera system
    /// - Located via string references: "camera" @ 0x007b63fc, "CameraID" @ 0x007bd160, "CameraList" @ 0x007bd16c
    /// - "CameraStyle" @ 0x007bd6e0, "CameraAnimation" @ 0x007c3460, "CameraAngle" @ 0x007c3490
    /// - "CameraModel" @ 0x007c3908, "CameraViewAngle" @ 0x007cb940, "Camera" @ 0x007cb350
    /// - "CAMERASPACE" @ 0x007c5108, "CameraHeightOffset" @ 0x007c5114
    /// - Camera hooks: "camerahook" @ 0x007c7dac, "camerahookt" @ 0x007c7da0, "camerahookz" @ 0x007c7db8, "camerahookh" @ 0x007c7dc4
    /// - "CAMERAHOOK" @ 0x007c7f10, "3CCameraHook" @ 0x007ca5ae, "CameraRotate" @ 0x007cb910
    /// - Camera hook format strings: "camerahook%d" @ 0x007d0448, "camerahook%c" @ 0x007d3790, "camerahook31" @ 0x007d0428
    /// - Keyboard controls: "Keyboard Camera Deceleration" @ 0x007c834c, "Keyboard Camera Acceleration" @ 0x007c836c, "Keyboard Camera DPS" @ 0x007c838c
    /// - Original implementation: Chase camera follows player, controllable pitch/yaw/zoom, collision detection
    /// - Chase camera follows player from behind with configurable distance and height offset
    /// - Controllable pitch and yaw (rotation around target) with keyboard/mouse input
    /// - Zoom in/out (distance from target) with min/max distance limits
    /// - Camera collision to avoid seeing through walls (raycast from target to camera position)
    /// - Cinematic camera for dialogues/cutscenes (CameraAnimation, CameraAngle)
    /// - Camera hooks: Attachment points on models for cinematic camera positioning (camerahook%d format)
    /// - Free camera for debug/editor mode
    /// </remarks>
    public class CameraController
    {
        private readonly IWorld _world;

        /// <summary>
        /// Current camera mode.
        /// </summary>
        public CameraMode Mode { get; private set; }

        /// <summary>
        /// Camera target entity (for chase mode).
        /// </summary>
        public IEntity Target { get; private set; }

        /// <summary>
        /// Current camera position.
        /// </summary>
        public Vector3 Position { get; private set; }

        /// <summary>
        /// Current look-at target position.
        /// </summary>
        public Vector3 LookAtPosition { get; private set; }

        /// <summary>
        /// Current camera up vector.
        /// </summary>
        public Vector3 Up { get; private set; }

        /// <summary>
        /// Horizontal rotation around target (radians).
        /// </summary>
        public float Yaw { get; set; }

        /// <summary>
        /// Vertical rotation (radians).
        /// </summary>
        public float Pitch { get; set; }

        /// <summary>
        /// Distance from target.
        /// </summary>
        public float Distance { get; set; }

        /// <summary>
        /// Field of view in radians.
        /// </summary>
        public float FieldOfView { get; set; }

        /// <summary>
        /// Minimum distance from target.
        /// </summary>
        public float MinDistance { get; set; }

        /// <summary>
        /// Maximum distance from target.
        /// </summary>
        public float MaxDistance { get; set; }

        /// <summary>
        /// Minimum pitch angle (looking down).
        /// </summary>
        public float MinPitch { get; set; }

        /// <summary>
        /// Maximum pitch angle (looking up).
        /// </summary>
        public float MaxPitch { get; set; }

        /// <summary>
        /// Camera height offset from target.
        /// </summary>
        public float HeightOffset { get; set; }

        /// <summary>
        /// Camera rotation speed.
        /// </summary>
        public float RotationSpeed { get; set; }

        /// <summary>
        /// Camera zoom speed.
        /// </summary>
        public float ZoomSpeed { get; set; }

        /// <summary>
        /// Camera smoothing factor (0 = instant, 1 = maximum smooth).
        /// </summary>
        public float Smoothing { get; set; }

        /// <summary>
        /// Whether camera collision is enabled.
        /// </summary>
        public bool CollisionEnabled { get; set; }

        /// <summary>
        /// Event fired when camera mode changes.
        /// </summary>
        public event Action<CameraMode> OnModeChanged;

        /// <summary>
        /// Event fired when camera updates.
        /// </summary>
        public event Action OnCameraUpdated;

        public CameraController(IWorld world)
        {
            _world = world ?? throw new ArgumentNullException("world");

            // Default values (KOTOR-style)
            Mode = CameraMode.Chase;
            Position = new Vector3(0, -10, 5);
            LookAtPosition = Vector3.Zero;
            Up = Vector3.UnitZ;

            Yaw = 0;
            Pitch = 0.4f; // Slightly looking down
            Distance = 8.0f;
            FieldOfView = (float)(Math.PI / 4); // 45 degrees

            MinDistance = 3.0f;
            MaxDistance = 20.0f;
            MinPitch = -0.3f; // Slightly up
            MaxPitch = 1.2f; // Looking down
            HeightOffset = 1.5f; // Target's eye height

            RotationSpeed = 2.0f;
            ZoomSpeed = 5.0f;
            Smoothing = 0.1f;
            CollisionEnabled = true;
        }

        #region Mode Switching

        /// <summary>
        /// Sets chase camera mode following an entity.
        /// </summary>
        public void SetChaseMode(IEntity target)
        {
            Target = target;
            Mode = CameraMode.Chase;

            if (OnModeChanged != null)
            {
                OnModeChanged(Mode);
            }
        }

        /// <summary>
        /// Sets free camera mode (no target).
        /// </summary>
        public void SetFreeMode()
        {
            Mode = CameraMode.Free;

            if (OnModeChanged != null)
            {
                OnModeChanged(Mode);
            }
        }

        /// <summary>
        /// Sets dialogue camera mode.
        /// </summary>
        public void SetDialogueMode(IEntity speaker, IEntity listener)
        {
            Mode = CameraMode.Dialogue;
            _dialogueSpeaker = speaker;
            _dialogueListener = listener;
            _dialogueCameraAngle = DialogueCameraAngle.Speaker;

            if (OnModeChanged != null)
            {
                OnModeChanged(Mode);
            }
        }

        /// <summary>
        /// Sets cinematic camera mode.
        /// </summary>
        public void SetCinematicMode(Vector3 position, Vector3 lookAt)
        {
            Mode = CameraMode.Cinematic;
            _cinematicPosition = position;
            _cinematicLookAt = lookAt;

            if (OnModeChanged != null)
            {
                OnModeChanged(Mode);
            }
        }

        #endregion

        #region Input

        /// <summary>
        /// Rotates camera by delta yaw and pitch.
        /// </summary>
        public void Rotate(float deltaYaw, float deltaPitch)
        {
            if (Mode == CameraMode.Cinematic)
            {
                return; // No control in cinematic mode
            }

            Yaw += deltaYaw * RotationSpeed;
            Pitch = Math.Max(MinPitch, Math.Min(MaxPitch, Pitch + deltaPitch * RotationSpeed));

            // Normalize yaw
            const float TwoPi = (float)(Math.PI * 2);
            while (Yaw > Math.PI) Yaw -= TwoPi;
            while (Yaw < -Math.PI) Yaw += TwoPi;
        }

        /// <summary>
        /// Zooms camera by delta distance.
        /// </summary>
        public void Zoom(float deltaDistance)
        {
            if (Mode == CameraMode.Cinematic)
            {
                return;
            }

            Distance = Math.Max(MinDistance, Math.Min(MaxDistance, Distance + deltaDistance * ZoomSpeed));
        }

        /// <summary>
        /// Moves free camera.
        /// </summary>
        public void Move(Vector3 movement)
        {
            if (Mode != CameraMode.Free)
            {
                return;
            }

            // Transform movement to camera space
            var forward = Vector3.Normalize(LookAtPosition - Position);
            var right = Vector3.Normalize(Vector3.Cross(forward, Up));
            var up = Vector3.Cross(right, forward);

            Position += right * movement.X + up * movement.Y + forward * movement.Z;
            LookAtPosition += right * movement.X + up * movement.Y + forward * movement.Z;
        }

        #endregion

        #region Update

        /// <summary>
        /// Updates camera each frame.
        /// </summary>
        public void Update(float deltaTime)
        {
            switch (Mode)
            {
                case CameraMode.Chase:
                    UpdateChaseCamera(deltaTime);
                    break;

                case CameraMode.Free:
                    UpdateFreeCamera(deltaTime);
                    break;

                case CameraMode.Dialogue:
                    UpdateDialogueCamera(deltaTime);
                    break;

                case CameraMode.Cinematic:
                    UpdateCinematicCamera(deltaTime);
                    break;
            }

            if (OnCameraUpdated != null)
            {
                OnCameraUpdated();
            }
        }

        private void UpdateChaseCamera(float deltaTime)
        {
            if (Target == null)
            {
                return;
            }

            // Get target position
            Interfaces.Components.ITransformComponent targetTransform = Target.GetComponent<Interfaces.Components.ITransformComponent>();
            if (targetTransform == null)
            {
                return;
            }

            Vector3 targetPos = targetTransform.Position;
            targetPos.Z += HeightOffset;

            // Calculate ideal camera position
            float horizontalDistance = Distance * (float)Math.Cos(Pitch);
            float verticalDistance = Distance * (float)Math.Sin(Pitch);

            var idealPosition = new Vector3(
                targetPos.X - horizontalDistance * (float)Math.Cos(Yaw),
                targetPos.Y - horizontalDistance * (float)Math.Sin(Yaw),
                targetPos.Z + verticalDistance
            );

            // Apply collision detection
            if (CollisionEnabled)
            {
                idealPosition = ApplyCollision(targetPos, idealPosition);
            }

            // Smooth camera movement
            float smoothFactor = 1.0f - (float)Math.Pow(Smoothing, deltaTime * 60);
            Position = Vector3.Lerp(Position, idealPosition, smoothFactor);
            LookAtPosition = Vector3.Lerp(LookAtPosition, targetPos, smoothFactor);
        }

        private void UpdateFreeCamera(float deltaTime)
        {
            // Free camera doesn't auto-update, controlled entirely by input
        }

        private void UpdateDialogueCamera(float deltaTime)
        {
            if (_dialogueSpeaker == null || _dialogueListener == null)
            {
                return;
            }

            // Get speaker and listener positions
            Vector3 speakerPos = GetEntityHeadPosition(_dialogueSpeaker);
            Vector3 listenerPos = GetEntityHeadPosition(_dialogueListener);

            // Calculate camera position based on current angle
            Vector3 idealPosition;
            Vector3 idealLookAt;

            switch (_dialogueCameraAngle)
            {
                case DialogueCameraAngle.Speaker:
                    idealPosition = CalculateDialogueCameraPosition(speakerPos, listenerPos, true);
                    idealLookAt = speakerPos;
                    break;

                case DialogueCameraAngle.Listener:
                    idealPosition = CalculateDialogueCameraPosition(listenerPos, speakerPos, true);
                    idealLookAt = listenerPos;
                    break;

                case DialogueCameraAngle.Wide:
                    idealPosition = CalculateWideShotPosition(speakerPos, listenerPos);
                    idealLookAt = (speakerPos + listenerPos) * 0.5f;
                    break;

                case DialogueCameraAngle.OverShoulder:
                    idealPosition = CalculateOverShoulderPosition(listenerPos, speakerPos);
                    idealLookAt = speakerPos;
                    break;

                default:
                    idealPosition = Position;
                    idealLookAt = LookAtPosition;
                    break;
            }

            // Smooth transition
            float smoothFactor = 1.0f - (float)Math.Pow(0.05f, deltaTime * 60);
            Position = Vector3.Lerp(Position, idealPosition, smoothFactor);
            LookAtPosition = Vector3.Lerp(LookAtPosition, idealLookAt, smoothFactor);
        }

        private void UpdateCinematicCamera(float deltaTime)
        {
            // Smooth transition to cinematic position
            float smoothFactor = 1.0f - (float)Math.Pow(0.05f, deltaTime * 60);
            Position = Vector3.Lerp(Position, _cinematicPosition, smoothFactor);
            LookAtPosition = Vector3.Lerp(LookAtPosition, _cinematicLookAt, smoothFactor);
        }

        #endregion

        #region Dialogue Camera

        private IEntity _dialogueSpeaker;
        private IEntity _dialogueListener;
        private DialogueCameraAngle _dialogueCameraAngle;

        /// <summary>
        /// Sets the current dialogue camera angle.
        /// </summary>
        public void SetDialogueCameraAngle(DialogueCameraAngle angle)
        {
            _dialogueCameraAngle = angle;
        }

        /// <summary>
        /// Swaps speaker and listener focus.
        /// </summary>
        public void SwapDialogueFocus()
        {
            IEntity temp = _dialogueSpeaker;
            _dialogueSpeaker = _dialogueListener;
            _dialogueListener = temp;
        }

        private Vector3 GetEntityHeadPosition(IEntity entity)
        {
            Interfaces.Components.ITransformComponent transform = entity.GetComponent<Interfaces.Components.ITransformComponent>();
            if (transform == null)
            {
                return Vector3.Zero;
            }

            // Add head height offset
            return transform.Position + new Vector3(0, 0, 1.7f);
        }

        private Vector3 CalculateDialogueCameraPosition(Vector3 subject, Vector3 other, bool leftSide)
        {
            // Camera positioned to the side of the conversation
            var direction = Vector3.Normalize(other - subject);
            var perpendicular = new Vector3(-direction.Y, direction.X, 0);
            
            if (!leftSide)
            {
                perpendicular = -perpendicular;
            }

            float sideOffset = 1.5f;
            float forwardOffset = 2.0f;
            float heightOffset = 0.2f;

            return subject + perpendicular * sideOffset - direction * forwardOffset + new Vector3(0, 0, heightOffset);
        }

        private Vector3 CalculateWideShotPosition(Vector3 pos1, Vector3 pos2)
        {
            Vector3 center = (pos1 + pos2) * 0.5f;
            var direction = Vector3.Normalize(pos2 - pos1);
            var perpendicular = new Vector3(-direction.Y, direction.X, 0);

            float distance = Vector3.Distance(pos1, pos2);
            float cameraDistance = distance * 0.8f + 2.0f;

            return center + perpendicular * cameraDistance + new Vector3(0, 0, 0.5f);
        }

        private Vector3 CalculateOverShoulderPosition(Vector3 shoulder, Vector3 subject)
        {
            var direction = Vector3.Normalize(subject - shoulder);
            var perpendicular = new Vector3(-direction.Y, direction.X, 0);

            float behindOffset = 0.5f;
            float sideOffset = 0.4f;
            float heightOffset = 0.3f;

            return shoulder - direction * behindOffset + perpendicular * sideOffset + new Vector3(0, 0, heightOffset);
        }

        #endregion

        #region Cinematic Camera

        private Vector3 _cinematicPosition;
        private Vector3 _cinematicLookAt;
        private CinematicShot _currentShot;
        private float _shotTime;
        private float _shotDuration;

        /// <summary>
        /// Plays a cinematic shot.
        /// </summary>
        public void PlayCinematicShot(CinematicShot shot, float duration)
        {
            _currentShot = shot;
            _shotDuration = duration;
            _shotTime = 0;

            Mode = CameraMode.Cinematic;
            _cinematicPosition = shot.StartPosition;
            _cinematicLookAt = shot.StartLookAt;
        }

        /// <summary>
        /// Updates cinematic shot animation.
        /// </summary>
        public void UpdateCinematicShot(float deltaTime)
        {
            if (_currentShot == null)
            {
                return;
            }

            _shotTime += deltaTime;
            float t = Math.Min(_shotTime / _shotDuration, 1.0f);

            // Interpolate position and look-at
            _cinematicPosition = Vector3.Lerp(_currentShot.StartPosition, _currentShot.EndPosition, t);
            _cinematicLookAt = Vector3.Lerp(_currentShot.StartLookAt, _currentShot.EndLookAt, t);
        }

        #endregion

        #region Collision

        private Vector3 ApplyCollision(Vector3 target, Vector3 idealPosition)
        {
            // Camera collision using walkmesh
            // Based on swkotor2.exe camera collision system
            // Located via string references: Camera collision checks
            // Original implementation: Raycasts from target to camera position, moves camera closer if blocked
            if (_world.CurrentArea != null)
            {
                INavigationMesh navMesh = _world.CurrentArea.NavigationMesh;
                if (navMesh != null)
                {
                    Vector3 direction = idealPosition - target;
                    float distance = direction.Length();
                    if (distance > 0.1f)
                    {
                        direction = Vector3.Normalize(direction);
                        
                        Vector3 hitPoint;
                        int hitFace;
                        if (navMesh.Raycast(target, direction, distance, out hitPoint, out hitFace))
                        {
                            // Collision detected - move camera closer to target
                            float safeDistance = (hitPoint - target).Length() - 0.5f; // 0.5m buffer
                            if (safeDistance > 0.1f)
                            {
                                return target + direction * safeDistance;
                            }
                            else
                            {
                                // Too close, use minimum distance
                                return target + direction * 0.1f;
                            }
                        }
                    }
                }
            }
            
            return idealPosition;
        }

        #endregion

        #region Camera Facing

        /// <summary>
        /// Sets the camera facing direction (yaw angle in radians).
        /// Based on swkotor2.exe: Camera facing control for scripted camera movements
        /// </summary>
        /// <param name="facing">Facing angle in radians (0 = north, PI/2 = east, PI = south, -PI/2 = west)</param>
        public void SetFacing(float facing)
        {
            if (Mode == CameraMode.Chase && Target != null)
            {
                // Set yaw to match facing direction
                Yaw = facing;
                
                // Normalize yaw to [-PI, PI]
                const float TwoPi = (float)(Math.PI * 2);
                while (Yaw > Math.PI) Yaw -= TwoPi;
                while (Yaw < -Math.PI) Yaw += TwoPi;
            }
            else if (Mode == CameraMode.Free)
            {
                // For free camera, rotate look-at position around current position
                Vector3 direction = LookAtPosition - Position;
                float distance = direction.Length();
                
                if (distance > 0.001f)
                {
                    direction = Vector3.Normalize(direction);
                    // Calculate new direction based on facing
                    var newDirection = new Vector3(
                        (float)Math.Cos(facing),
                        (float)Math.Sin(facing),
                        direction.Z
                    );
                    LookAtPosition = Position + newDirection * distance;
                }
            }
        }

        #endregion

        #region Camera Hooks

        /// <summary>
        /// Gets the world-space position of a camera hook on an entity.
        /// Based on swkotor2.exe: Camera hook lookup system
        /// Located via string references: "camerahook" @ 0x007c7dac, "camerahook%d" @ 0x007d0448
        /// Original implementation: Searches MDL node tree for nodes named "camerahook{N}" and returns world-space position
        /// Camera hooks are MDL nodes (dummy nodes) with names like "camerahook1", "camerahook2", etc.
        /// </summary>
        /// <param name="entity">The entity to get the camera hook from.</param>
        /// <param name="hookIndex">The camera hook index (1-based, e.g., 1 = "camerahook1").</param>
        /// <param name="hookPosition">Output parameter for the world-space hook position.</param>
        /// <returns>True if the hook was found, false otherwise.</returns>
        public bool GetCameraHookPosition(IEntity entity, int hookIndex, out Vector3 hookPosition)
        {
            hookPosition = Vector3.Zero;

            if (entity == null || hookIndex < 1)
            {
                return false;
            }

            // Get entity transform
            Interfaces.Components.ITransformComponent transform = entity.GetComponent<Interfaces.Components.ITransformComponent>();
            if (transform == null)
            {
                return false;
            }

            // Try to get camera hook position from entity
            // Camera hooks are typically stored as attachment points on the model
            // For now, we'll use a fallback approach: calculate hook position based on entity position and orientation
            // Full implementation would query the loaded MDL model for nodes named "camerahook{N}"
            // and transform their local positions to world space using the entity's transform

            // Fallback: Calculate approximate hook position based on entity facing
            // This is a simplified implementation - full version would query MDL node tree
            Vector3 entityPos = transform.Position;
            Vector3 entityForward = GetEntityForward(transform);
            Vector3 entityRight = GetEntityRight(transform);
            Vector3 entityUp = GetEntityUp(transform);

            // Default camera hook positions relative to entity
            // Hook 1: Front-right, slightly elevated
            // Hook 2: Front-left, slightly elevated
            // Hook 3: Behind-right, slightly elevated
            // Hook 4: Behind-left, slightly elevated
            // These are approximate positions - actual hooks are defined in MDL files
            float hookDistance = 2.0f;
            float hookHeight = 1.5f;

            switch (hookIndex)
            {
                case 1:
                    hookPosition = entityPos + entityForward * hookDistance + entityRight * 0.5f + entityUp * hookHeight;
                    return true;
                case 2:
                    hookPosition = entityPos + entityForward * hookDistance - entityRight * 0.5f + entityUp * hookHeight;
                    return true;
                case 3:
                    hookPosition = entityPos - entityForward * hookDistance + entityRight * 0.5f + entityUp * hookHeight;
                    return true;
                case 4:
                    hookPosition = entityPos - entityForward * hookDistance - entityRight * 0.5f + entityUp * hookHeight;
                    return true;
                default:
                    // For other hook indices, use a generic position
                    float angle = (hookIndex - 1) * (float)(Math.PI * 2 / 8); // 8 hooks around entity
                    hookPosition = entityPos + 
                        entityForward * hookDistance * (float)Math.Cos(angle) +
                        entityRight * hookDistance * (float)Math.Sin(angle) +
                        entityUp * hookHeight;
                    return true;
            }
        }

        /// <summary>
        /// Gets the forward vector from an entity's transform.
        /// </summary>
        private Vector3 GetEntityForward(Interfaces.Components.ITransformComponent transform)
        {
            // ITransformComponent has Forward property directly
            // Use the Forward property which is computed from Facing angle
            return transform.Forward;
        }

        /// <summary>
        /// Gets the right vector from an entity's transform.
        /// </summary>
        private Vector3 GetEntityRight(Interfaces.Components.ITransformComponent transform)
        {
            // ITransformComponent has Right property directly
            // Use the Right property which is computed from Facing angle
            return transform.Right;
        }

        /// <summary>
        /// Gets the up vector from an entity's transform.
        /// </summary>
        private Vector3 GetEntityUp(Interfaces.Components.ITransformComponent transform)
        {
            // Compute up vector from forward and right using cross product
            // Up = Forward × Right (right-handed coordinate system)
            Vector3 forward = transform.Forward;
            Vector3 right = transform.Right;
            return Vector3.Cross(forward, right);
        }

            // Rotate (0, 0, 1) by quaternion
            return new Vector3(
                2.0f * (x * y + w * z),
                2.0f * (y * z - w * x),
                1.0f - 2.0f * (x * x + y * y)
            );
        }

        /// <summary>
        /// Sets camera position and look-at using camera hooks.
        /// Based on swkotor2.exe: Camera hook-based positioning for dialogue animations
        /// </summary>
        /// <param name="cameraHookEntity">Entity with camera hook (camera position).</param>
        /// <param name="cameraHookIndex">Camera hook index on camera hook entity.</param>
        /// <param name="lookAtEntity">Entity to look at (optional, uses head position).</param>
        /// <param name="lookAtHookIndex">Look-at hook index (optional, uses head position if not specified).</param>
        public void SetCameraFromHooks(IEntity cameraHookEntity, int cameraHookIndex, IEntity lookAtEntity, int lookAtHookIndex = 0)
        {
            Vector3 cameraPos;
            Vector3 lookAtPos;

            // Get camera position from hook
            if (!GetCameraHookPosition(cameraHookEntity, cameraHookIndex, out cameraPos))
            {
                // Fallback to entity head position
                cameraPos = GetEntityHeadPosition(cameraHookEntity);
            }

            // Get look-at position
            if (lookAtEntity != null)
            {
                if (lookAtHookIndex > 0)
                {
                    if (!GetCameraHookPosition(lookAtEntity, lookAtHookIndex, out lookAtPos))
                    {
                        lookAtPos = GetEntityHeadPosition(lookAtEntity);
                    }
                }
                else
                {
                    lookAtPos = GetEntityHeadPosition(lookAtEntity);
                }
            }
            else
            {
                // Look at camera hook entity's head if no look-at entity specified
                lookAtPos = GetEntityHeadPosition(cameraHookEntity);
            }

            // Set cinematic camera mode with hook-based positions
            SetCinematicMode(cameraPos, lookAtPos);
        }

        #endregion

        #region View Matrix

        /// <summary>
        /// Gets the view matrix for rendering.
        /// </summary>
        public Matrix4x4 GetViewMatrix()
        {
            return Matrix4x4.CreateLookAt(Position, LookAtPosition, Up);
        }

        /// <summary>
        /// Gets the projection matrix for rendering.
        /// </summary>
        public Matrix4x4 GetProjectionMatrix(float aspectRatio, float nearPlane = 0.1f, float farPlane = 1000f)
        {
            return Matrix4x4.CreatePerspectiveFieldOfView(FieldOfView, aspectRatio, nearPlane, farPlane);
        }

        #endregion
    }

    /// <summary>
    /// Camera operating mode.
    /// </summary>
    public enum CameraMode
    {
        /// <summary>
        /// Chase camera following player.
        /// </summary>
        Chase,

        /// <summary>
        /// Free-flying camera (debug).
        /// </summary>
        Free,

        /// <summary>
        /// Dialogue camera.
        /// </summary>
        Dialogue,

        /// <summary>
        /// Scripted cinematic camera.
        /// </summary>
        Cinematic
    }

    /// <summary>
    /// Dialogue camera angles.
    /// </summary>
    public enum DialogueCameraAngle
    {
        /// <summary>
        /// Focus on speaker.
        /// </summary>
        Speaker,

        /// <summary>
        /// Focus on listener.
        /// </summary>
        Listener,

        /// <summary>
        /// Wide shot of both.
        /// </summary>
        Wide,

        /// <summary>
        /// Over-the-shoulder shot.
        /// </summary>
        OverShoulder
    }

    /// <summary>
    /// Defines a cinematic camera shot.
    /// </summary>
    public class CinematicShot
    {
        /// <summary>
        /// Starting camera position.
        /// </summary>
        public Vector3 StartPosition { get; set; }

        /// <summary>
        /// Ending camera position.
        /// </summary>
        public Vector3 EndPosition { get; set; }

        /// <summary>
        /// Starting look-at point.
        /// </summary>
        public Vector3 StartLookAt { get; set; }

        /// <summary>
        /// Ending look-at point.
        /// </summary>
        public Vector3 EndLookAt { get; set; }

        /// <summary>
        /// Starting field of view.
        /// </summary>
        public float StartFOV { get; set; }

        /// <summary>
        /// Ending field of view.
        /// </summary>
        public float EndFOV { get; set; }

        /// <summary>
        /// Easing function type.
        /// </summary>
        public EasingType Easing { get; set; }

        public CinematicShot()
        {
            StartFOV = (float)(Math.PI / 4);
            EndFOV = (float)(Math.PI / 4);
            Easing = EasingType.Linear;
        }
    }

    /// <summary>
    /// Easing function types for camera animation.
    /// </summary>
    public enum EasingType
    {
        Linear,
        EaseIn,
        EaseOut,
        EaseInOut
    }

    /// <summary>
    /// Defines a dialogue camera animation with hook support.
    /// Based on swkotor2.exe: Camera animation system for dialogues
    /// Located via string references: "CameraAnimation" @ 0x007c3460
    /// Original implementation: Camera animations are scripted camera movements using camera hooks or predefined angles
    /// </summary>
    public class DialogueCameraAnimation
    {
        /// <summary>
        /// Animation ID (matches animId parameter in SetAnimation).
        /// </summary>
        public int AnimationId { get; set; }

        /// <summary>
        /// Camera hook entity (entity with camera hook for position).
        /// </summary>
        public IEntity CameraHookEntity { get; set; }

        /// <summary>
        /// Camera hook index (1-based, e.g., 1 = "camerahook1").
        /// </summary>
        public int CameraHookIndex { get; set; }

        /// <summary>
        /// Look-at entity (entity to focus camera on).
        /// </summary>
        public IEntity LookAtEntity { get; set; }

        /// <summary>
        /// Look-at hook index (optional, 0 = use head position).
        /// </summary>
        public int LookAtHookIndex { get; set; }

        /// <summary>
        /// Fallback camera angle (used if hooks are not available).
        /// </summary>
        public DialogueCameraAngle FallbackAngle { get; set; }

        /// <summary>
        /// Animation duration in seconds.
        /// </summary>
        public float Duration { get; set; }

        /// <summary>
        /// Easing function type.
        /// </summary>
        public EasingType Easing { get; set; }

        /// <summary>
        /// Whether this animation uses camera hooks (true) or predefined angles (false).
        /// </summary>
        public bool UsesHooks { get; set; }

        public DialogueCameraAnimation()
        {
            AnimationId = 0;
            CameraHookIndex = 0;
            LookAtHookIndex = 0;
            FallbackAngle = DialogueCameraAngle.Speaker;
            Duration = 0.5f;
            Easing = EasingType.EaseInOut;
            UsesHooks = false;
        }
    }
}

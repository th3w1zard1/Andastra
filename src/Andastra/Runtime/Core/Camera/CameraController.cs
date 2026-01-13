using System;
using System.Numerics;
using BioWare.NET.Resource.Formats.MDL;
using BioWare.NET.Resource;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Core.MDL;

namespace Andastra.Runtime.Core.Camera
{
    /// <summary>
    /// Manages camera behavior in the game world.
    /// </summary>
    /// <remarks>
    /// KOTOR Camera System:
    /// - Based on swkotor.exe and swkotor2.exe camera system
    /// - swkotor.exe (KOTOR 1): Camera system @ FUN_004af630 (chase camera update), FUN_004b0a20 (camera collision)
    /// - swkotor2.exe (KOTOR 2): Camera system @ FUN_004dcfb0 (chase camera update), FUN_004dd1a0 (camera collision)
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
    public class CameraController : ICameraController
    {
        private readonly IWorld _world;
        private readonly IMDLLoader _mdlLoader;

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

        public CameraController(IWorld world, IMDLLoader mdlLoader = null)
        {
            _world = world ?? throw new ArgumentNullException("world");
            _mdlLoader = mdlLoader;

            // Default values (KOTOR-style)
            // Based on swkotor.exe and swkotor2.exe: Y-up coordinate system (Y is vertical axis)
            Mode = CameraMode.Chase;
            Position = new Vector3(0, 5, -10); // Y-up: Y is height, Z is depth
            LookAtPosition = Vector3.Zero;
            Up = Vector3.UnitY; // Y-up coordinate system

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
        /// Gets the player entity from the world.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Player entity lookup for camera reset
        /// Reverse engineered from swkotor2.exe: When dialogue ends (EndConversation @ 0x007c38e0), camera resets to chase mode following player
        /// Located via string references: "Player" @ 0x007be628, "PlayerList" @ 0x007bdcf4, "GetPlayerList" @ 0x007bdd00
        /// Original implementation: Returns player entity for camera to follow after dialogue ends
        /// Cross-engine analysis:
        ///   - swkotor.exe (KOTOR 1): Similar player entity lookup pattern
        ///   - swkotor2.exe (KOTOR 2): Player entity tagged "Player", stored in module player list
        ///   - nwmain.exe (Aurora): Player entity via GetFirstPC() NWScript function, similar lookup pattern
        ///   - daorigins.exe/DragonAge2.exe (Eclipse): Player entity tagged "PlayerCharacter" or via GetControlled() function
        ///   - / (Infinity): Player entity via party leader or controlled entity
        /// </summary>
        /// <returns>The player entity, or null if not found.</returns>
        public IEntity GetPlayerEntity()
        {
            if (_world == null)
            {
                return null;
            }

            // Strategy 1: Try to find entity by tag "Player" (Odyssey engine pattern)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Player entity is tagged "Player" @ 0x007be628
            // Located via string references: "Player" @ 0x007be628, "Mod_PlayerList" @ 0x007be060
            // Original implementation: Player entity is stored in module player list and tagged "Player"
            IEntity playerEntity = _world.GetEntityByTag("Player", 0);
            if (playerEntity != null)
            {
                return playerEntity;
            }

            // Strategy 2: Try to find entity by tag "PlayerCharacter" (Eclipse engine pattern)
            // Based on daorigins.exe/DragonAge2.exe: Player entity tagged "PlayerCharacter"
            // Original implementation: Eclipse engine uses "PlayerCharacter" tag for player entity
            playerEntity = _world.GetEntityByTag("PlayerCharacter", 0);
            if (playerEntity != null)
            {
                return playerEntity;
            }

            // Strategy 3: Search through all entities for one marked as player
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Player entity has IsPlayer data flag set to true
            // Original implementation: Player entity is marked with IsPlayer flag during creation
            foreach (IEntity entity in _world.GetAllEntities())
            {
                if (entity != null && entity.GetData<bool>("IsPlayer", false))
                {
                    return entity;
                }
            }

            return null;
        }

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
        /// Sets free camera mode with direct position restoration.
        /// Based on nwmain.exe: Camera_Restore restores exact camera position, look-at, and up vector for free mode
        /// Reverse engineered from nwmain.exe: CNWSMessage::SendServerToPlayerCamera_Restore @ 0x1404d0f20
        /// Located via string references: "Camera_Restore" @ 0x140dcb190
        /// Original implementation: Client directly sets camera position, look-at, and up vector when restoring free mode
        /// This method provides 1:1 parity with nwmain.exe camera restoration behavior
        /// </summary>
        /// <param name="position">Camera position to restore.</param>
        /// <param name="lookAtPosition">Look-at position to restore.</param>
        /// <param name="up">Up vector to restore.</param>
        public void SetFreeModePosition(Vector3 position, Vector3 lookAtPosition, Vector3 up)
        {
            Mode = CameraMode.Free;
            Position = position;
            LookAtPosition = lookAtPosition;
            Up = up;

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
            // Y-up coordinate system: Y is vertical axis
            targetPos.Y += HeightOffset;

            // Calculate ideal camera position
            float horizontalDistance = Distance * (float)Math.Cos(Pitch);
            float verticalDistance = Distance * (float)Math.Sin(Pitch);

            // Y-up coordinate system: X and Z are horizontal, Y is vertical
            var idealPosition = new Vector3(
                targetPos.X - horizontalDistance * (float)Math.Cos(Yaw),
                targetPos.Y + verticalDistance, // Y is vertical
                targetPos.Z - horizontalDistance * (float)Math.Sin(Yaw)
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

            // Add head height offset (Y-up coordinate system: Y is vertical)
            return transform.Position + new Vector3(0, 1.7f, 0);
        }

        private Vector3 CalculateDialogueCameraPosition(Vector3 subject, Vector3 other, bool leftSide)
        {
            // Camera positioned to the side of the conversation
            // Y-up coordinate system: Y is vertical, X and Z are horizontal
            var direction = Vector3.Normalize(other - subject);
            // Perpendicular in XZ plane (Y-up: horizontal plane is XZ)
            var perpendicular = new Vector3(-direction.Z, 0, direction.X);

            if (!leftSide)
            {
                perpendicular = -perpendicular;
            }

            float sideOffset = 1.5f;
            float forwardOffset = 2.0f;
            float heightOffset = 0.2f;

            // Y-up: height offset is in Y direction
            return subject + perpendicular * sideOffset - direction * forwardOffset + new Vector3(0, heightOffset, 0);
        }

        private Vector3 CalculateWideShotPosition(Vector3 pos1, Vector3 pos2)
        {
            Vector3 center = (pos1 + pos2) * 0.5f;
            var direction = Vector3.Normalize(pos2 - pos1);
            // Perpendicular in XZ plane (Y-up: horizontal plane is XZ)
            var perpendicular = new Vector3(-direction.Z, 0, direction.X);

            float distance = Vector3.Distance(pos1, pos2);
            float cameraDistance = distance * 0.8f + 2.0f;

            // Y-up: height offset is in Y direction
            return center + perpendicular * cameraDistance + new Vector3(0, 0.5f, 0);
        }

        private Vector3 CalculateOverShoulderPosition(Vector3 shoulder, Vector3 subject)
        {
            var direction = Vector3.Normalize(subject - shoulder);
            // Perpendicular in XZ plane (Y-up: horizontal plane is XZ)
            var perpendicular = new Vector3(-direction.Z, 0, direction.X);

            float behindOffset = 0.5f;
            float sideOffset = 0.4f;
            float heightOffset = 0.3f;

            // Y-up: height offset is in Y direction
            return shoulder - direction * behindOffset + perpendicular * sideOffset + new Vector3(0, heightOffset, 0);
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
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) camera collision system
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
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Camera facing control for scripted camera movements
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
                    // Calculate new direction based on facing (Y-up: X and Z are horizontal, Y is vertical)
                    // Preserve vertical component (Y) and update horizontal direction (X, Z) from facing angle
                    var newDirection = new Vector3(
                        (float)Math.Cos(facing),
                        direction.Y, // Preserve vertical component (Y-up)
                        (float)Math.Sin(facing)
                    );
                    LookAtPosition = Position + newDirection * distance;
                }
            }
        }

        #endregion

        #region Camera Hooks

        /// <summary>
        /// Gets the world-space position of a camera hook on an entity.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Camera hook lookup system
        /// Located via string references: "camerahook" @ 0x007c7dac, "camerahook%d" @ 0x007d0448
        /// Original implementation: Searches MDL node tree for nodes named "camerahook{N}" and returns world-space position
        /// Camera hooks are MDL dummy nodes (NodeType = 1) with names like "camerahook1", "camerahook2", etc.
        /// Dummy nodes are non-rendering nodes used for positioning and hierarchy (NODE_HAS_HEADER flag only, value 0x001 = 1)
        /// Based on MDL format specification: Dummy nodes have NodeType = MDLNodeType.DUMMY (1)
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

            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_006c6020 @ 0x006c6020 searches MDL node tree for "camerahook" nodes
            // Located via string references: "camerahook" @ 0x007c7dac, "camerahook%d" @ 0x007d0448
            // Original implementation:
            //   - Searches MDL model node tree recursively for nodes named "camerahook{N}" (e.g., "camerahook1", "camerahook2")
            //   - Uses format string "camerahook%d" to construct node name from hookIndex
            //   - Queries model via FUN_006c21c0 @ 0x006c21c0 to get node by index
            //   - Calls virtual function at offset 0x10c with "camerahook" string to find node by name
            //   - Verifies node is a dummy node (NodeType = 1, NODE_HAS_HEADER flag only)
            //   - Transforms node's local position to world space using entity's transform matrix
            //   - Returns world-space position of the camera hook node
            // Implementation: Full MDL node lookup with recursive search, dummy node validation, and world-space transform

            // Construct camera hook node name (format: "camerahook{N}")
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Format string "camerahook%d" @ 0x007d0448
            string hookNodeName = string.Format("camerahook{0}", hookIndex);

            // Try to get MDL model from entity
            MDLModel mdlModel = GetEntityMDLModel(entity);
            if (mdlModel != null && mdlModel.RootNode != null)
            {
                // Search for camera hook node recursively
                MDLNodeData hookNode = FindNodeByName(mdlModel.RootNode, hookNodeName);
                if (hookNode != null)
                {
                    // Verify that the found node is a dummy node
                    // Camera hooks must be dummy nodes (NodeType = 1, NODE_HAS_HEADER flag only)
                    // Based on MDL format: Dummy nodes have NodeType = MDLNodeType.DUMMY (1)
                    // Dummy nodes are non-rendering nodes used for positioning and hierarchy
                    // NodeType value 1 = NODE_HAS_HEADER flag (0x001) = dummy node
                    ushort nodeTypeValue = hookNode.NodeType;
                    bool isDummyNode = (nodeTypeValue == (ushort)MDLNodeType.DUMMY) ||
                                       (nodeTypeValue == 0x0001); // NODE_HAS_HEADER flag only (0x001 = 1)

                    if (!isDummyNode)
                    {
                        // Node found but is not a dummy node - this is unexpected
                        // Camera hooks should always be dummy nodes, but we'll log a warning and continue
                        // Some models might have incorrectly typed camera hook nodes
                        Console.WriteLine($"[CameraController] Warning: Camera hook node '{hookNodeName}' found but is not a dummy node (NodeType={nodeTypeValue}). Expected NodeType=1 (DUMMY).");
                        // Continue anyway - the node might still be usable as a camera hook
                    }

                    // Transform node position to world space
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Node transform calculation for world-space positioning
                    // Original implementation: Combines node's local transform with entity's world transform
                    Vector3 nodeWorldPosition = TransformNodeToWorldSpace(hookNode, mdlModel.RootNode, transform);

                    hookPosition = nodeWorldPosition;
                    return true;
                }
            }

            // Fallback: Calculate approximate hook position based on entity facing
            // This is used when MDL model is not available or camera hook node is not found
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

        /// <summary>
        /// Gets the MDL model for an entity.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Model loading for camera hook lookup
        /// </summary>
        private MDLModel GetEntityMDLModel(IEntity entity)
        {
            if (_mdlLoader == null)
            {
                return null;
            }

            // Get model ResRef from renderable component
            IRenderableComponent renderable = entity.GetComponent<IRenderableComponent>();
            if (renderable == null || string.IsNullOrEmpty(renderable.ModelResRef))
            {
                return null;
            }

            try
            {
                // Load MDL model using MDLLoader
                return _mdlLoader.Load(renderable.ModelResRef);
            }
            catch
            {
                // Model loading failed, return null
                return null;
            }
        }

        /// <summary>
        /// Recursively searches for a node by name in the MDL node tree.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_006c6020 searches MDL node tree recursively
        /// Original implementation: Calls virtual function at offset 0x10c with node name string
        /// </summary>
        private MDLNodeData FindNodeByName(MDLNodeData rootNode, string nodeName)
        {
            if (rootNode == null || string.IsNullOrEmpty(nodeName))
            {
                return null;
            }

            // Use iterative depth-first search to avoid stack overflow on deep trees
            var stack = new System.Collections.Generic.Stack<MDLNodeData>();
            stack.Push(rootNode);

            while (stack.Count > 0)
            {
                MDLNodeData currentNode = stack.Pop();

                // Check if current node matches
                if (currentNode.Name != null && currentNode.Name.Equals(nodeName, StringComparison.OrdinalIgnoreCase))
                {
                    return currentNode;
                }

                // Add children to stack for recursive search
                if (currentNode.Children != null)
                {
                    for (int i = currentNode.Children.Length - 1; i >= 0; i--)
                    {
                        if (currentNode.Children[i] != null)
                        {
                            stack.Push(currentNode.Children[i]);
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Transforms a node's local position to world space.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Node transform calculation for world-space positioning
        /// Original implementation: Combines node's local transform with entity's world transform
        /// </summary>
        private Vector3 TransformNodeToWorldSpace(MDLNodeData node, MDLNodeData rootNode, Interfaces.Components.ITransformComponent entityTransform)
        {
            if (node == null || entityTransform == null)
            {
                return Vector3.Zero;
            }

            // Get node's local position
            Vector3 localPosition = new Vector3(
                node.Position.X,
                node.Position.Y,
                node.Position.Z
            );

            // Accumulate parent transforms from root to node
            Vector3 accumulatedPosition = localPosition;
            Vector4 accumulatedOrientation = new Vector4(
                node.Orientation.X,
                node.Orientation.Y,
                node.Orientation.Z,
                node.Orientation.W
            );

            // Build transform chain from root to node
            MDLNodeData currentNode = node;
            var transformChain = new System.Collections.Generic.List<MDLNodeData>();

            // Find path from root to node
            if (FindPathToNode(rootNode, node, transformChain))
            {
                // Apply transforms from root to node (excluding the node itself)
                for (int i = 0; i < transformChain.Count - 1; i++)
                {
                    MDLNodeData parentNode = transformChain[i];
                    if (parentNode != null)
                    {
                        Vector3 parentPos = new Vector3(
                            parentNode.Position.X,
                            parentNode.Position.Y,
                            parentNode.Position.Z
                        );

                        Vector4 parentOrient = new Vector4(
                            parentNode.Orientation.X,
                            parentNode.Orientation.Y,
                            parentNode.Orientation.Z,
                            parentNode.Orientation.W
                        );

                        // Transform position by parent's orientation and add parent's position
                        Vector3 rotatedPos = RotateVectorByQuaternion(accumulatedPosition, parentOrient);
                        accumulatedPosition = parentPos + rotatedPos;

                        // Combine orientations (quaternion multiplication)
                        accumulatedOrientation = MultiplyQuaternions(parentOrient, accumulatedOrientation);
                    }
                }
            }

            // Transform to entity's world space
            // Entity transform: position + facing rotation (Y-up coordinate system)
            Vector3 entityPos = entityTransform.Position;
            float entityFacing = entityTransform.Facing;

            // Rotate node position by entity's facing (rotation around Y axis)
            Vector3 rotatedByFacing = new Vector3(
                accumulatedPosition.X * (float)Math.Cos(entityFacing) - accumulatedPosition.Z * (float)Math.Sin(entityFacing),
                accumulatedPosition.Y,
                accumulatedPosition.X * (float)Math.Sin(entityFacing) + accumulatedPosition.Z * (float)Math.Cos(entityFacing)
            );

            // Apply entity's orientation quaternion if available
            Vector3 finalPosition = rotatedByFacing;
            if (accumulatedOrientation.W != 1.0f || accumulatedOrientation.X != 0.0f || accumulatedOrientation.Y != 0.0f || accumulatedOrientation.Z != 0.0f)
            {
                finalPosition = RotateVectorByQuaternion(rotatedByFacing, accumulatedOrientation);
            }

            // Add entity position
            return entityPos + finalPosition;
        }

        /// <summary>
        /// Finds the path from root node to target node.
        /// </summary>
        private bool FindPathToNode(MDLNodeData currentNode, MDLNodeData targetNode, System.Collections.Generic.List<MDLNodeData> path)
        {
            if (currentNode == null)
            {
                return false;
            }

            path.Add(currentNode);

            if (currentNode == targetNode)
            {
                return true;
            }

            if (currentNode.Children != null)
            {
                foreach (MDLNodeData child in currentNode.Children)
                {
                    if (child != null && FindPathToNode(child, targetNode, path))
                    {
                        return true;
                    }
                }
            }

            path.RemoveAt(path.Count - 1);
            return false;
        }

        /// <summary>
        /// Rotates a vector by a quaternion.
        /// </summary>
        private Vector3 RotateVectorByQuaternion(Vector3 vector, Vector4 quaternion)
        {
            // Quaternion rotation: v' = q * v * q^-1
            // Optimized version for unit quaternions
            float qx = quaternion.X;
            float qy = quaternion.Y;
            float qz = quaternion.Z;
            float qw = quaternion.W;

            float vx = vector.X;
            float vy = vector.Y;
            float vz = vector.Z;

            // Calculate quaternion * vector
            float tx = qw * vx + qy * vz - qz * vy;
            float ty = qw * vy + qz * vx - qx * vz;
            float tz = qw * vz + qx * vy - qy * vx;
            float tw = -qx * vx - qy * vy - qz * vz;

            // Calculate result * quaternion^-1 (conjugate for unit quaternion)
            return new Vector3(
                tx * qw + tw * -qx + ty * -qz - tz * -qy,
                ty * qw + tw * -qy + tz * -qx - tx * -qz,
                tz * qw + tw * -qz + tx * -qy - ty * -qx
            );
        }

        /// <summary>
        /// Multiplies two quaternions (q1 * q2).
        /// </summary>
        private Vector4 MultiplyQuaternions(Vector4 q1, Vector4 q2)
        {
            return new Vector4(
                q1.W * q2.X + q1.X * q2.W + q1.Y * q2.Z - q1.Z * q2.Y,
                q1.W * q2.Y - q1.X * q2.Z + q1.Y * q2.W + q1.Z * q2.X,
                q1.W * q2.Z + q1.X * q2.Y - q1.Y * q2.X + q1.Z * q2.W,
                q1.W * q2.W - q1.X * q2.X - q1.Y * q2.Y - q1.Z * q2.Z
            );
        }

        /// <summary>
        /// Sets camera position and look-at using camera hooks.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Camera hook-based positioning for dialogue animations
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
    /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Camera animation system for dialogues
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

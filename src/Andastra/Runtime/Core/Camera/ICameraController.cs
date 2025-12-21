using System;
using System.Numerics;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Core.Camera
{
    /// <summary>
    /// Interface for camera control in the game world.
    /// Provides abstraction for camera positioning, movement, and mode switching.
    /// </summary>
    /// <remarks>
    /// Camera Controller Interface:
    /// - Based on swkotor.exe and swkotor2.exe camera system
    /// - Provides unified interface for camera control across all BioWare engines
    /// - Supports multiple camera modes: Chase, Free, Dialogue, Cinematic
    /// - Chase camera follows player with configurable distance, height offset, pitch/yaw
    /// - Free camera for debug/editor mode
    /// - Dialogue camera for conversation scenes
    /// - Cinematic camera for scripted cutscenes
    /// - Camera collision detection to avoid seeing through walls
    /// - Camera hooks support for MDL-based camera positioning
    /// </remarks>
    public interface ICameraController
    {
        /// <summary>
        /// Current camera mode.
        /// </summary>
        CameraMode Mode { get; }

        /// <summary>
        /// Camera target entity (for chase mode).
        /// </summary>
        IEntity Target { get; }

        /// <summary>
        /// Current camera position.
        /// </summary>
        Vector3 Position { get; }

        /// <summary>
        /// Current look-at target position.
        /// </summary>
        Vector3 LookAtPosition { get; }

        /// <summary>
        /// Current camera up vector.
        /// </summary>
        Vector3 Up { get; }

        /// <summary>
        /// Horizontal rotation around target (radians).
        /// </summary>
        float Yaw { get; set; }

        /// <summary>
        /// Vertical rotation (radians).
        /// </summary>
        float Pitch { get; set; }

        /// <summary>
        /// Distance from target.
        /// </summary>
        float Distance { get; set; }

        /// <summary>
        /// Field of view in radians.
        /// </summary>
        float FieldOfView { get; set; }

        /// <summary>
        /// Minimum distance from target.
        /// </summary>
        float MinDistance { get; set; }

        /// <summary>
        /// Maximum distance from target.
        /// </summary>
        float MaxDistance { get; set; }

        /// <summary>
        /// Minimum pitch angle (looking down).
        /// </summary>
        float MinPitch { get; set; }

        /// <summary>
        /// Maximum pitch angle (looking up).
        /// </summary>
        float MaxPitch { get; set; }

        /// <summary>
        /// Camera height offset from target.
        /// </summary>
        float HeightOffset { get; set; }

        /// <summary>
        /// Camera rotation speed.
        /// </summary>
        float RotationSpeed { get; set; }

        /// <summary>
        /// Camera zoom speed.
        /// </summary>
        float ZoomSpeed { get; set; }

        /// <summary>
        /// Camera smoothing factor (0 = instant, 1 = maximum smooth).
        /// </summary>
        float Smoothing { get; set; }

        /// <summary>
        /// Whether camera collision is enabled.
        /// </summary>
        bool CollisionEnabled { get; set; }

        /// <summary>
        /// Event fired when camera mode changes.
        /// </summary>
        event Action<CameraMode> OnModeChanged;

        /// <summary>
        /// Event fired when camera updates.
        /// </summary>
        event Action OnCameraUpdated;

        /// <summary>
        /// Gets the player entity from the world.
        /// </summary>
        /// <returns>The player entity, or null if not found.</returns>
        IEntity GetPlayerEntity();

        /// <summary>
        /// Sets chase camera mode following an entity.
        /// </summary>
        void SetChaseMode(IEntity target);

        /// <summary>
        /// Sets free camera mode (no target).
        /// </summary>
        void SetFreeMode();

        /// <summary>
        /// Sets free camera mode with direct position restoration.
        /// Based on nwmain.exe: Camera_Restore restores exact camera position, look-at, and up vector for free mode
        /// </summary>
        /// <param name="position">Camera position to restore.</param>
        /// <param name="lookAtPosition">Look-at position to restore.</param>
        /// <param name="up">Up vector to restore.</param>
        void SetFreeModePosition(Vector3 position, Vector3 lookAtPosition, Vector3 up);

        /// <summary>
        /// Sets dialogue camera mode.
        /// </summary>
        void SetDialogueMode(IEntity speaker, IEntity listener);

        /// <summary>
        /// Sets cinematic camera mode.
        /// </summary>
        void SetCinematicMode(Vector3 position, Vector3 lookAt);

        /// <summary>
        /// Rotates camera by delta yaw and pitch.
        /// </summary>
        void Rotate(float deltaYaw, float deltaPitch);

        /// <summary>
        /// Zooms camera by delta distance.
        /// </summary>
        void Zoom(float deltaDistance);

        /// <summary>
        /// Moves free camera.
        /// </summary>
        void Move(Vector3 movement);

        /// <summary>
        /// Updates camera each frame.
        /// </summary>
        void Update(float deltaTime);

        /// <summary>
        /// Sets the current dialogue camera angle.
        /// </summary>
        void SetDialogueCameraAngle(DialogueCameraAngle angle);

        /// <summary>
        /// Swaps speaker and listener focus.
        /// </summary>
        void SwapDialogueFocus();

        /// <summary>
        /// Sets the camera facing direction (yaw angle in radians).
        /// </summary>
        void SetFacing(float facing);

        /// <summary>
        /// Gets the world-space position of a camera hook on an entity.
        /// </summary>
        /// <param name="entity">The entity to get the camera hook from.</param>
        /// <param name="hookIndex">The camera hook index (1-based, e.g., 1 = "camerahook1").</param>
        /// <param name="hookPosition">Output parameter for the world-space hook position.</param>
        /// <returns>True if the hook was found, false otherwise.</returns>
        bool GetCameraHookPosition(IEntity entity, int hookIndex, out Vector3 hookPosition);

        /// <summary>
        /// Sets camera position and look-at using camera hooks.
        /// </summary>
        void SetCameraFromHooks(IEntity cameraHookEntity, int cameraHookIndex, IEntity lookAtEntity, int lookAtHookIndex = 0);

        /// <summary>
        /// Plays a cinematic shot.
        /// </summary>
        void PlayCinematicShot(CinematicShot shot, float duration);

        /// <summary>
        /// Updates cinematic shot animation.
        /// </summary>
        void UpdateCinematicShot(float deltaTime);

        /// <summary>
        /// Gets the view matrix for rendering.
        /// </summary>
        Matrix4x4 GetViewMatrix();

        /// <summary>
        /// Gets the projection matrix for rendering.
        /// </summary>
        Matrix4x4 GetProjectionMatrix(float aspectRatio, float nearPlane = 0.1f, float farPlane = 1000f);
    }
}


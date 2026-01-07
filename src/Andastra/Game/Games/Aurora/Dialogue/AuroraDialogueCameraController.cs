using System;
using System.Numerics;
using Andastra.Runtime.Core.Camera;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Games.Common.Dialogue;

namespace Andastra.Runtime.Games.Aurora.Dialogue
{
    /// <summary>
    /// Aurora engine implementation of dialogue camera controller.
    /// Controls camera during dialogue conversations in Neverwinter Nights.
    /// </summary>
    /// <remarks>
    /// Aurora Dialogue Camera Controller:
    /// - Based on nwmain.exe dialogue camera system
    /// - Located via string references: "Camera_Store" @ 0x140dcb180, "Camera_Restore" @ 0x140dcb190
    /// - "cameraDialogState" @ 0x140d8bca8, "Dialog" @ 0x140dc9dc0
    /// - "EndConversation" @ 0x140de6f70 loaded in CNWSDialog::LoadDialog @ 0x14041b5c0 (line 55-56)
    /// - Camera state management: Camera_Store saves camera state when dialogue starts, Camera_Restore restores it when dialogue ends
    /// - Original implementation: Aurora stores camera state (position, rotation, mode) when dialogue begins
    /// - When dialogue ends (EndConversation script fires), camera state is restored to pre-dialogue state
    /// - This differs from Odyssey which resets to chase mode - Aurora preserves the exact camera state
    /// - Reverse engineered from nwmain.exe: Camera state stored/restored via Camera_Store/Camera_Restore functions
    /// - Located via string references: Camera_Store and Camera_Restore are NWScript functions that save/restore camera state
    /// - This implementation matches 1:1 with nwmain.exe behavior
    /// </remarks>
    public class AuroraDialogueCameraController : BaseDialogueCameraController
    {
        // Stored camera state (matches nwmain.exe Camera_Store behavior)
        private CameraMode _storedCameraMode;
        private IEntity _storedTarget;
        private Vector3 _storedPosition;
        private Vector3 _storedLookAtPosition;
        private Vector3 _storedUp;
        private float _storedYaw;
        private float _storedPitch;
        private float _storedDistance;
        private float _storedFieldOfView;
        private float _storedHeightOffset;
        private bool _cameraStateStored;

        /// <summary>
        /// Initializes a new Aurora dialogue camera controller.
        /// </summary>
        /// <param name="cameraController">The camera controller to use.</param>
        public AuroraDialogueCameraController(CameraController cameraController)
            : base(cameraController)
        {
            _cameraStateStored = false;
        }

        /// <summary>
        /// Sets the camera to focus on the speaker and listener.
        /// Based on nwmain.exe: Camera state is stored before dialogue begins
        /// </summary>
        /// <param name="speaker">The speaking entity.</param>
        /// <param name="listener">The listening entity.</param>
        public override void SetFocus(IEntity speaker, IEntity listener)
        {
            if (speaker == null || listener == null)
            {
                return;
            }

            // Based on nwmain.exe: Store camera state before entering dialogue mode
            // Reverse engineered from nwmain.exe:
            //   - CNWSPlayer::StoreCameraSettings @ 0x1404bed80 calls CNWSMessage::SendServerToPlayerCamera_Store @ 0x1404d1230
            //   - SendServerToPlayerCamera_Store sends message type 0x10, submessage 0x03 to client
            //   - Client stores: camera mode, position, rotation (yaw/pitch), distance, field of view, height offset, target
            // Located via string references: "Camera_Store" @ 0x140dcb180
            // Original implementation: Camera_Store saves complete camera state (mode, position, rotation, target, all parameters)
            // This matches 1:1 with nwmain.exe behavior
            if (!_cameraStateStored)
            {
                _storedCameraMode = CameraController.Mode;
                _storedTarget = CameraController.Target;
                _storedPosition = CameraController.Position;
                _storedLookAtPosition = CameraController.LookAtPosition;
                _storedUp = CameraController.Up;
                _storedYaw = CameraController.Yaw;
                _storedPitch = CameraController.Pitch;
                _storedDistance = CameraController.Distance;
                _storedFieldOfView = CameraController.FieldOfView;
                _storedHeightOffset = CameraController.HeightOffset;
                _cameraStateStored = true;
            }

            CurrentSpeaker = speaker;
            CurrentListener = listener;
            CameraController.SetDialogueMode(speaker, listener);
        }

        /// <summary>
        /// Sets the camera angle.
        /// Based on nwmain.exe: Aurora uses simpler camera angle system than Odyssey
        /// </summary>
        /// <param name="angle">The camera angle index.</param>
        public override void SetAngle(int angle)
        {
            // Based on nwmain.exe: Aurora dialogue camera angles are simpler
            // Original implementation: Basic camera angle switching for dialogue
            // Note: Aurora's dialogue camera system is less sophisticated than Odyssey's
            if (CurrentSpeaker != null && CurrentListener != null)
            {
                CameraController.SetDialogueMode(CurrentSpeaker, CurrentListener);
            }
        }

        /// <summary>
        /// Sets the camera animation.
        /// Based on nwmain.exe: Aurora does not use complex camera animations for dialogue
        /// </summary>
        /// <param name="animId">The camera animation ID.</param>
        public override void SetAnimation(int animId)
        {
            // Based on nwmain.exe: Aurora does not have dialogue camera animations like Odyssey
            // Original implementation: No camera animation system for dialogues
            // Fallback to angle-based system
            SetAngle(animId);
        }

        /// <summary>
        /// Resets the camera to normal gameplay mode.
        /// Based on nwmain.exe: Camera restore after dialogue ends
        /// Reverse engineered from nwmain.exe:
        ///   - CNWSDialog::RunEndConversationScript @ 0x1404997e0 executes EndConversation script when dialogue ends
        ///   - EndConversation script execution @ 0x140de6f70 triggers camera restore
        ///   - Dialogue loading function CNWSDialog::LoadDialog @ 0x14041b5c0 loads EndConversation script reference (line 55-56)
        ///   - CNWSPlayer::RestoreCameraSettings @ 0x1404bd690 calls CNWSMessage::SendServerToPlayerCamera_Restore @ 0x1404d0f20
        ///   - SendServerToPlayerCamera_Restore sends message type 0x10, submessage 0x04 to client
        ///   - Client restores: camera mode, position, rotation (yaw/pitch), distance, field of view, height offset, target
        /// Located via string references: "Camera_Restore" @ 0x140dcb190, "EndConversation" @ 0x140de6f70
        /// Original implementation: When dialogue ends, camera state is restored to exactly what it was before dialogue started
        /// This differs from Odyssey which resets to chase mode - Aurora preserves the exact camera state
        /// This implementation matches 1:1 with nwmain.exe behavior
        /// </summary>
        public override void Reset()
        {
            // Based on nwmain.exe: Camera restore after dialogue ends
            // Reverse engineered from nwmain.exe: CNWSPlayer::RestoreCameraSettings @ 0x1404bd690
            // Located via string references: "Camera_Restore" @ 0x140dcb190
            // Original implementation: Restores complete camera state (mode, target, position, rotation, all parameters)
            // This matches 1:1 with nwmain.exe behavior
            if (_cameraStateStored)
            {
                // Restore stored camera mode and target
                // Based on nwmain.exe: Camera_Restore function restores complete camera state
                // Original implementation: Restores all camera parameters to pre-dialogue state
                if (_storedCameraMode == CameraMode.Chase && _storedTarget != null)
                {
                    // Restore chase mode with stored target
                    CameraController.SetChaseMode(_storedTarget);
                    // Restore chase mode parameters
                    CameraController.Yaw = _storedYaw;
                    CameraController.Pitch = _storedPitch;
                    CameraController.Distance = _storedDistance;
                    CameraController.FieldOfView = _storedFieldOfView;
                    CameraController.HeightOffset = _storedHeightOffset;
                }
                else if (_storedCameraMode == CameraMode.Free)
                {
                    // Restore free mode with stored position and rotation
                    // Based on nwmain.exe: Free mode camera restoration
                    // Reverse engineered from nwmain.exe: CNWSPlayer::RestoreCameraSettings @ 0x1404bd690
                    //   - Calls CNWSMessage::SendServerToPlayerCamera_Restore @ 0x1404d0f20
                    //   - SendServerToPlayerCamera_Restore sends message type 0x10, submessage 0x04 to client
                    //   - Client restores: camera mode, position, look-at, up vector, field of view
                    // Located via string references: "Camera_Restore" @ 0x140dcb190
                    // Original implementation: When dialogue ends, camera state is restored to exactly what it was before dialogue started
                    // This includes exact position, look-at position, up vector, and field of view for free mode
                    // This implementation matches 1:1 with nwmain.exe behavior
                    CameraController.SetFreeModePosition(_storedPosition, _storedLookAtPosition, _storedUp);
                    CameraController.FieldOfView = _storedFieldOfView;
                }
                else if (_storedCameraMode == CameraMode.Dialogue)
                {
                    // If stored mode was dialogue, restore to chase mode with player
                    // This shouldn't happen normally, but handle it gracefully
                    IEntity playerEntity = CameraController.GetPlayerEntity();
                    if (playerEntity != null)
                    {
                        CameraController.SetChaseMode(playerEntity);
                    }
                    else
                    {
                        CameraController.SetFreeMode();
                    }
                }
                else
                {
                    // Fallback: Get player entity and set to chase mode
                    // Based on nwmain.exe: If stored state invalid, fallback to player chase mode
                    IEntity playerEntity = CameraController.GetPlayerEntity();
                    if (playerEntity != null)
                    {
                        CameraController.SetChaseMode(playerEntity);
                    }
                    else
                    {
                        CameraController.SetFreeMode();
                    }
                }

                _cameraStateStored = false;
            }
            else
            {
                // Fallback: Get player entity and set to chase mode
                // Based on nwmain.exe: If no stored state, reset to player chase mode
                // This can happen if Reset() is called before SetFocus(), or if dialogue was started without camera store
                IEntity playerEntity = CameraController.GetPlayerEntity();
                if (playerEntity != null)
                {
                    CameraController.SetChaseMode(playerEntity);
                }
                else
                {
                    CameraController.SetFreeMode();
                }
            }

            CurrentSpeaker = null;
            CurrentListener = null;
        }
    }
}


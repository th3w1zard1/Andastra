using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.Core.Camera;
using Andastra.Runtime.Core.Dialogue;
using Andastra.Runtime.Core.Interfaces;
using DialogueCameraAngle = Andastra.Runtime.Core.Camera.DialogueCameraAngle;

namespace Andastra.Runtime.Stride.Camera
{
    /// <summary>
    /// Stride implementation of IDialogueCameraController.
    /// Controls camera during dialogue conversations.
    /// </summary>
    /// <remarks>
    /// Dialogue Camera Controller (Stride Implementation):
    /// - Based on swkotor2.exe dialogue camera system
    /// - Located via string references: "CameraAnimation" @ 0x007c3460, "CameraAngle" @ 0x007c3490
    /// - "CameraModel" @ 0x007c3908, "CameraViewAngle" @ 0x007cb940
    /// - Camera hooks: "camerahook" @ 0x007c7dac, "camerahookt" @ 0x007c7da0, "camerahookz" @ 0x007c7db8, "camerahookh" @ 0x007c7dc4
    /// - "CAMERAHOOK" @ 0x007c7f10, "3CCameraHook" @ 0x007ca5ae, "CameraRotate" @ 0x007cb910
    /// - Original implementation: Dialogue camera focuses on speaker/listener with configurable angles
    /// - Camera angles: Speaker focus, listener focus, wide shot, over-the-shoulder
    /// - Camera animations: Smooth transitions between angles, scripted camera movements
    /// - Camera hooks: Attachment points on models for precise camera positioning
    /// - This implementation uses Andastra.Runtime.Core.Camera.CameraController for actual camera control
    /// </remarks>
    public class StrideDialogueCameraController : IDialogueCameraController
    {
        private readonly CameraController _cameraController;
        private readonly Dictionary<int, DialogueCameraAnimation> _cameraAnimations;
        private IEntity _currentSpeaker;
        private IEntity _currentListener;

        /// <summary>
        /// Initializes a new dialogue camera controller.
        /// </summary>
        /// <param name="cameraController">The camera controller to use.</param>
        public StrideDialogueCameraController(CameraController cameraController)
        {
            _cameraController = cameraController ?? throw new ArgumentNullException(nameof(cameraController));
            _cameraAnimations = new Dictionary<int, DialogueCameraAnimation>();
            InitializeDefaultAnimations();
        }

        /// <summary>
        /// Initializes default camera animations (angles 0-3).
        /// Based on swkotor2.exe: Default camera animation mappings
        /// </summary>
        private void InitializeDefaultAnimations()
        {
            // Animation 0: Speaker focus (fallback to angle)
            _cameraAnimations[0] = new DialogueCameraAnimation
            {
                AnimationId = 0,
                FallbackAngle = DialogueCameraAngle.Speaker,
                Duration = 0.5f,
                Easing = EasingType.EaseInOut,
                UsesHooks = false
            };

            // Animation 1: Listener focus (fallback to angle)
            _cameraAnimations[1] = new DialogueCameraAnimation
            {
                AnimationId = 1,
                FallbackAngle = DialogueCameraAngle.Listener,
                Duration = 0.5f,
                Easing = EasingType.EaseInOut,
                UsesHooks = false
            };

            // Animation 2: Wide shot (fallback to angle)
            _cameraAnimations[2] = new DialogueCameraAnimation
            {
                AnimationId = 2,
                FallbackAngle = DialogueCameraAngle.Wide,
                Duration = 0.5f,
                Easing = EasingType.EaseInOut,
                UsesHooks = false
            };

            // Animation 3: Over-shoulder (fallback to angle)
            _cameraAnimations[3] = new DialogueCameraAnimation
            {
                AnimationId = 3,
                FallbackAngle = DialogueCameraAngle.OverShoulder,
                Duration = 0.5f,
                Easing = EasingType.EaseInOut,
                UsesHooks = false
            };
        }

        /// <summary>
        /// Registers a camera animation with hook support.
        /// Based on swkotor2.exe: Camera animation registration system
        /// </summary>
        /// <param name="animation">The camera animation to register.</param>
        public void RegisterAnimation(DialogueCameraAnimation animation)
        {
            if (animation == null)
            {
                throw new ArgumentNullException(nameof(animation));
            }

            _cameraAnimations[animation.AnimationId] = animation;
        }

        /// <summary>
        /// Sets the camera to focus on the speaker and listener.
        /// </summary>
        /// <param name="speaker">The speaking entity.</param>
        /// <param name="listener">The listening entity.</param>
        public void SetFocus(IEntity speaker, IEntity listener)
        {
            if (speaker == null || listener == null)
            {
                return;
            }

            // Based on swkotor2.exe: Dialogue camera focus implementation
            // Located via string references: "CameraAnimation" @ 0x007c3460, "CameraAngle" @ 0x007c3490
            // Original implementation: Sets camera to dialogue mode with speaker/listener focus
            // Camera defaults to speaker focus angle
            _currentSpeaker = speaker;
            _currentListener = listener;
            _cameraController.SetDialogueMode(speaker, listener);
            _cameraController.SetDialogueCameraAngle(DialogueCameraAngle.Speaker);
        }

        /// <summary>
        /// Sets the camera angle.
        /// </summary>
        /// <param name="angle">The camera angle index (0 = speaker, 1 = listener, 2 = wide, 3 = over-shoulder).</param>
        public void SetAngle(int angle)
        {
            // Based on swkotor2.exe: Camera angle selection
            // Located via string references: "CameraAngle" @ 0x007c3490
            // Original implementation: Camera angle index maps to DialogueCameraAngle enum
            DialogueCameraAngle cameraAngle;
            switch (angle)
            {
                case 0:
                    cameraAngle = DialogueCameraAngle.Speaker;
                    break;
                case 1:
                    cameraAngle = DialogueCameraAngle.Listener;
                    break;
                case 2:
                    cameraAngle = DialogueCameraAngle.Wide;
                    break;
                case 3:
                    cameraAngle = DialogueCameraAngle.OverShoulder;
                    break;
                default:
                    cameraAngle = DialogueCameraAngle.Speaker;
                    break;
            }

            _cameraController.SetDialogueCameraAngle(cameraAngle);
        }

        /// <summary>
        /// Sets the camera animation.
        /// Based on swkotor2.exe: Camera animation system with hook support
        /// Located via string references: "CameraAnimation" @ 0x007c3460
        /// Original implementation: Camera animations are scripted camera movements using camera hooks or predefined angles
        /// Camera animation IDs map to predefined camera movements, hooks, or angles
        /// Full implementation supports camera hooks for precise positioning from MDL models
        /// </summary>
        /// <param name="animId">The camera animation ID.</param>
        public void SetAnimation(int animId)
        {
            DialogueCameraAnimation animation;
            if (!_cameraAnimations.TryGetValue(animId, out animation))
            {
                // Unknown animation ID - fallback to angle-based system for IDs 0-3
                if (animId >= 0 && animId <= 3)
                {
                    SetAngle(animId);
                }
                return;
            }

            // Check if animation uses camera hooks
            if (animation.UsesHooks && animation.CameraHookEntity != null)
            {
                // Use camera hook-based positioning
                // Based on swkotor2.exe: Camera hook positioning for dialogue animations
                // Located via string references: "camerahook" @ 0x007c7dac, "camerahook%d" @ 0x007d0448
                // Original implementation: Queries MDL model for nodes named "camerahook{N}" and uses their world-space positions
                IEntity lookAtEntity = animation.LookAtEntity ?? _currentSpeaker;
                _cameraController.SetCameraFromHooks(
                    animation.CameraHookEntity,
                    animation.CameraHookIndex,
                    lookAtEntity,
                    animation.LookAtHookIndex
                );
            }
            else
            {
                // Use fallback angle-based system
                // Based on swkotor2.exe: Fallback to predefined camera angles when hooks are not available
                // Original implementation: Uses DialogueCameraAngle enum for standard camera positions
                _cameraController.SetDialogueCameraAngle(animation.FallbackAngle);
            }
        }

        /// <summary>
        /// Resets the camera to normal gameplay mode.
        /// Based on swkotor2.exe: Camera reset to chase mode
        /// Located via string references: Camera mode switching
        /// Original implementation: Returns camera to chase mode following player
        /// Reverse engineered from swkotor2.exe: Camera reset after dialogue ends returns to chase mode with player as target
        /// </summary>
        public void Reset()
        {
            // Get player entity from world via camera controller
            IEntity playerEntity = _cameraController.GetPlayerEntity();
            
            if (playerEntity != null)
            {
                // Reset to chase mode following player
                _cameraController.SetChaseMode(playerEntity);
            }
            else
            {
                // Fallback to free mode if player entity not found (shouldn't happen in normal gameplay)
                _cameraController.SetFreeMode();
            }
        }
    }
}


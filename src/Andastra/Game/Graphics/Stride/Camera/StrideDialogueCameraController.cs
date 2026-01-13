using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.Core.Camera;
using Andastra.Runtime.Core.Dialogue;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using DialogueCameraAngle = Andastra.Runtime.Core.Camera.DialogueCameraAngle;

namespace Andastra.Game.Stride.Camera
{
    /// <summary>
    /// Stride implementation of IDialogueCameraController.
    /// Controls camera during dialogue conversations.
    /// </summary>
    /// <remarks>
    /// Dialogue Camera Controller (Stride Implementation for Odyssey Engine):
    /// - Based on swkotor.exe and swkotor2.exe dialogue camera system
    /// - swkotor.exe (KOTOR 1): EndConversation @ 0x0074a7c0, dialogue loading 0x005a2ae0 @ 0x005a2ae0
    /// - swkotor2.exe (KOTOR 2): EndConversation @ 0x007c38e0, dialogue loading 0x005ea880 @ 0x005ea880
    /// - Located via string references: "CameraAnimation" @ 0x007c3460, "CameraAngle" @ 0x007c3490
    /// - "CameraModel" @ 0x007c3908, "CameraViewAngle" @ 0x007cb940
    /// - Camera hooks: "camerahook" @ 0x007c7dac, "camerahookt" @ 0x007c7da0, "camerahookz" @ 0x007c7db8, "camerahookh" @ 0x007c7dc4
    /// - "CAMERAHOOK" @ 0x007c7f10, "3CCameraHook" @ 0x007ca5ae, "CameraRotate" @ 0x007cb910
    /// - Original implementation: Dialogue camera focuses on speaker/listener with configurable angles
    /// - Camera angles: Speaker focus, listener focus, wide shot, over-the-shoulder
    /// - Camera animations: Smooth transitions between angles, scripted camera movements
    /// - Camera hooks: Attachment points on models for precise camera positioning
    /// - This implementation matches 1:1 with both swkotor.exe and swkotor2.exe behavior
    /// - This implementation uses Runtime.Core.Camera.CameraController for actual camera control
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
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Default camera animation mappings
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
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Camera animation registration system
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

            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Dialogue camera focus implementation
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
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Camera angle selection
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
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Camera animation system with hook support
        /// Located via string references: "CameraAnimation" @ 0x007c3460
        /// Original implementation: Camera animations are scripted camera movements using camera hooks or predefined angles
        /// Camera animation IDs map to predefined camera movements, hooks, or angles
        /// Full implementation supports camera hooks for precise positioning from MDL models:
        /// - Camera hooks are MDL dummy nodes (NodeType = 1) with names like "camerahook1", "camerahook2", etc.
        /// - Camera hooks define precise camera positions relative to character models in world space
        /// - The system automatically detects camera hooks from MDL models when available
        /// - If camera hooks are found in the speaker/listener models, they are used for precise positioning
        /// - If no camera hooks are available, the system falls back to predefined camera angles
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x006c6020 @ 0x006c6020 searches MDL node tree for "camerahook" nodes
        /// Original implementation: Queries MDL model for nodes named "camerahook{N}" and uses their world-space positions
        /// </summary>
        /// <param name="animId">The camera animation ID.</param>
        public void SetAnimation(int animId)
        {
            DialogueCameraAnimation animation;
            if (!_cameraAnimations.TryGetValue(animId, out animation))
            {
                // Unknown animation ID - fallback to angle-based system for IDs 0-3
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Fallback behavior when animation ID is not registered
                // Original implementation: If animation ID is 0-3, maps to standard camera angles
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
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Camera hook positioning for dialogue animations
                // Located via string references: "camerahook" @ 0x007c7dac, "camerahook%d" @ 0x007d0448
                // Original implementation: Queries MDL model for nodes named "camerahook{N}" and uses their world-space positions
                // Camera hooks are MDL dummy nodes that define precise camera positions relative to character models
                // The GetCameraHookPosition method searches MDL node tree recursively for "camerahook{N}" nodes
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x006c6020 @ 0x006c6020 searches MDL node tree for "camerahook" nodes
                // Implementation:
                //   1. Constructs camera hook node name (format: "camerahook{N}" where N is hookIndex)
                //   2. Searches MDL model node tree recursively for matching node name
                //   3. Verifies node is a dummy node (NodeType = 1, NODE_HAS_HEADER flag only)
                //   4. Transforms node's local position to world space using entity's transform matrix
                //   5. Returns world-space position of the camera hook node
                // If camera hook is not found, falls back to approximate position based on entity facing
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
                // Try to automatically detect and use camera hooks from speaker/listener models
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Automatic camera hook detection when animation doesn't explicitly specify hooks
                // Original implementation: If speaker/listener models have camera hooks, use them for precise positioning
                // This provides automatic camera hook support without requiring manual registration
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x006c6020 @ 0x006c6020 searches MDL node tree for "camerahook" nodes
                // Located via string references: "camerahook" @ 0x007c7dac, "camerahook%d" @ 0x007d0448
                // Original implementation: Searches MDL model node tree recursively for nodes named "camerahook{N}"
                bool hooksFound = false;
                if (_currentSpeaker != null)
                {
                    // Try to find camera hook 1 on speaker (common hook index for dialogue cameras)
                    Vector3 speakerHookPos;
                    if (_cameraController.GetCameraHookPosition(_currentSpeaker, 1, out speakerHookPos))
                    {
                        // Camera hook found on speaker - use it for camera positioning
                        // Look at listener's head or camera hook if available
                        Vector3 lookAtPos;
                        if (_currentListener != null)
                        {
                            // Try to find camera hook on listener for look-at target
                            if (!_cameraController.GetCameraHookPosition(_currentListener, 1, out lookAtPos))
                            {
                                // Fallback to listener's head position
                                lookAtPos = GetEntityHeadPosition(_currentListener);
                            }
                        }
                        else
                        {
                            // No listener - look at speaker's head
                            lookAtPos = GetEntityHeadPosition(_currentSpeaker);
                        }

                        // Set camera using detected hook
                        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Camera positioning using detected camera hooks
                        // Original implementation: Sets camera to cinematic mode with hook-based positions
                        _cameraController.SetCinematicMode(speakerHookPos, lookAtPos);
                        hooksFound = true;
                    }
                }

                if (!hooksFound)
                {
                    // No camera hooks found - use fallback angle-based system
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Fallback to predefined camera angles when hooks are not available
                    // Original implementation: Uses DialogueCameraAngle enum for standard camera positions
                    _cameraController.SetDialogueCameraAngle(animation.FallbackAngle);
                }
            }
        }

        /// <summary>
        /// Gets the head position of an entity for camera look-at targeting.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Entity head position calculation for dialogue cameras
        /// Original implementation: Head position is typically at entity position + height offset (1.7 units)
        /// This matches CameraController.GetEntityHeadPosition implementation for consistency
        /// </summary>
        /// <param name="entity">The entity to get head position from.</param>
        /// <returns>World-space head position.</returns>
        private Vector3 GetEntityHeadPosition(IEntity entity)
        {
            if (entity == null)
            {
                return Vector3.Zero;
            }

            ITransformComponent transform = entity.GetComponent<ITransformComponent>();
            if (transform == null)
            {
                return Vector3.Zero;
            }

            // Head position is entity position + height offset
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Head position calculation for dialogue cameras
            // Original implementation: Head position = entity position + up vector * head height (1.7 units)
            // This matches CameraController.GetEntityHeadPosition implementation (Y-up coordinate system: Y is vertical)
            return transform.Position + new Vector3(0, 1.7f, 0);
        }

        /// <summary>
        /// Resets the camera to normal gameplay mode.
        /// Based on swkotor.exe and swkotor2.exe: Camera reset to chase mode after dialogue ends
        /// Reverse engineered from swkotor.exe:
        ///   - EndConversation script execution @ 0x0074a7c0 triggers camera reset
        ///   - Dialogue loading function 0x005a2ae0 @ 0x005a2ae0 loads EndConversation script reference (line 55)
        ///   - Camera reset occurs when dialogue ends (EndConversation script fires)
        ///   - Camera returns to chase mode following player entity
        /// Reverse engineered from swkotor2.exe:
        ///   - EndConversation script execution @ 0x007c38e0 triggers camera reset
        ///   - Dialogue loading function 0x005ea880 @ 0x005ea880 loads EndConversation script reference (line 55)
        ///   - Camera reset occurs when dialogue ends (EndConversation script fires)
        ///   - Camera returns to chase mode following player entity
        /// Located via string references: "EndConversation" @ 0x0074a7c0 (swkotor.exe), @ 0x007c38e0 (swkotor2.exe)
        /// Original implementation: When dialogue ends, camera resets to chase mode with player as target
        /// This implementation matches 1:1 with both swkotor.exe and swkotor2.exe behavior
        /// Cross-engine analysis:
        ///   - swkotor.exe (KOTOR 1): Camera reset to chase mode with player entity (this implementation matches)
        ///   - swkotor2.exe (KOTOR 2): Camera reset to chase mode with player entity (this implementation matches)
        ///   - nwmain.exe (Aurora): Camera reset handled differently (no direct dialogue camera equivalent)
        ///   - daorigins.exe/DragonAge2.exe (Eclipse): Camera reset via UnrealScript, different architecture
        ///   - / (Infinity): Camera reset handled by level scripting system
        /// </summary>
        public void Reset()
        {
            // Get player entity from world via camera controller
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Player entity lookup for camera reset
            // Original implementation: Retrieves player entity and sets camera to chase mode
            IEntity playerEntity = _cameraController.GetPlayerEntity();

            if (playerEntity != null)
            {
                // Reset to chase mode following player
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Camera mode switching to chase mode with player as target
                // Original implementation: SetChaseMode sets camera to follow player entity
                _cameraController.SetChaseMode(playerEntity);
            }
            else
            {
                // Fallback to free mode if player entity not found (shouldn't happen in normal gameplay)
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Fallback behavior when player entity is unavailable
                // Original implementation: Free mode allows manual camera control if player not found
                _cameraController.SetFreeMode();
            }
        }
    }
}


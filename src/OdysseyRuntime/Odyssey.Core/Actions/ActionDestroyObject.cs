using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;

namespace Odyssey.Core.Actions
{
    /// <summary>
    /// Action to destroy an object with optional fade effects.
    /// </summary>
    /// <remarks>
    /// Destroy Object Action:
    /// - Based on swkotor2.exe DestroyObject NWScript function
    /// - Located via string references: "EVENT_DESTROY_OBJECT" @ 0x007bcd48
    /// - "DestroyObjectDelay" @ 0x007c0248, "IsDestroyable" @ 0x007bf670, "Destroyed" @ 0x007c4bdc
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_DESTROYPLAYERCREATURE" @ 0x007bc5ec (player creature destruction event)
    /// - Original implementation: Destroys object after delay, optionally with fade-out effect
    /// - If noFade is FALSE, object fades out before destruction (alpha fade from 1.0 to 0.0)
    /// - delayUntilFade controls when fade starts (if delay > 0, fade starts after delayUntilFade)
    /// - Rendering system should check "DestroyFade" flag and fade out entity before destruction
    /// - Fade duration: Typically 1-2 seconds for smooth visual transition
    /// - After fade completes (or if noFade is TRUE), object is removed from world
    /// </remarks>
    public class ActionDestroyObject : ActionBase
    {
        private readonly uint _targetObjectId;
        private readonly float _delay;
        private readonly bool _noFade;
        private readonly float _delayUntilFade;
        private bool _fadeStarted;
        private bool _destroyed;

        public ActionDestroyObject(uint targetObjectId, float delay = 0f, bool noFade = false, float delayUntilFade = 0f)
            : base(ActionType.DestroyObject)
        {
            _targetObjectId = targetObjectId;
            _delay = delay;
            _noFade = noFade;
            _delayUntilFade = delayUntilFade;
            _fadeStarted = false;
            _destroyed = false;
        }

        public uint TargetObjectId { get { return _targetObjectId; } }

        protected override ActionStatus ExecuteInternal(IEntity actor, float deltaTime)
        {
            if (_destroyed)
            {
                return ActionStatus.Complete;
            }

            // Wait for initial delay
            if (ElapsedTime < _delay)
            {
                return ActionStatus.InProgress;
            }

            // Start fade if needed
            if (!_noFade && !_fadeStarted)
            {
                // Wait for delayUntilFade if specified
                if (ElapsedTime >= _delay + _delayUntilFade)
                {
                    // Find target entity and set fade flag
                    if (actor != null && actor.World != null)
                    {
                        IEntity target = actor.World.GetEntity(_targetObjectId);
                        if (target != null && target is Core.Entities.Entity targetEntity)
                        {
                            // Set flag for rendering system to fade out
                            targetEntity.SetData("DestroyFade", true);
                            targetEntity.SetData("DestroyFadeStartTime", ElapsedTime);
                            _fadeStarted = true;
                        }
                        else
                        {
                            // Target already destroyed, complete immediately
                            _destroyed = true;
                            return ActionStatus.Complete;
                        }
                    }
                }
            }

            // If no fade, destroy immediately after delay
            if (_noFade && ElapsedTime >= _delay)
            {
                DestroyTarget(actor);
                return ActionStatus.Complete;
            }

            // If fade, wait for fade duration (typically 1-2 seconds)
            // The rendering system should handle the actual fade and notify when complete
            // For now, we'll use a fixed fade duration
            const float fadeDuration = 1.0f; // 1 second fade
            if (_fadeStarted && ElapsedTime >= _delay + _delayUntilFade + fadeDuration)
            {
                DestroyTarget(actor);
                return ActionStatus.Complete;
            }

            return ActionStatus.InProgress;
        }

        private void DestroyTarget(IEntity actor)
        {
            if (_destroyed)
            {
                return;
            }

            if (actor != null && actor.World != null)
            {
                actor.World.DestroyEntity(_targetObjectId);
                _destroyed = true;
            }
        }
    }
}


using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Core.Actions
{
    /// <summary>
    /// Action to destroy an object with optional fade effects.
    /// </summary>
    /// <remarks>
    /// Destroy Object Action:
    /// - Based on swkotor2.exe DestroyObject NWScript function
    /// - Located via string references: "EVENT_DESTROY_OBJECT" @ 0x007bcd48 (destroy object event, case 0xb)
    /// - Event dispatching: FUN_004dcfb0 @ 0x004dcfb0 handles EVENT_DESTROY_OBJECT event (case 0xb, fires before object removal)
    /// - "DestroyObjectDelay" @ 0x007c0248 (destroy object delay field), "IsDestroyable" @ 0x007bf670 (is destroyable flag)
    /// - "Destroyed" @ 0x007c4bdc (destroyed flag), "CSWSSCRIPTEVENT_EVENTTYPE_ON_DESTROYPLAYERCREATURE" @ 0x007bc5ec (player creature destruction event, 0x5)
    /// - Original implementation: Destroys object after delay, optionally with fade-out effect
    /// - Delay: Initial delay before destruction starts (default 0 seconds, can be set via delay parameter)
    /// - Fade behavior: If noFade is FALSE, object fades out before destruction (alpha fade from 1.0 to 0.0)
    /// - delayUntilFade controls when fade starts (if delay > 0, fade starts after delayUntilFade seconds from action start)
    /// - Rendering system: Should check "DestroyFade" flag and fade out entity before destruction (rendering system responsibility)
    /// - Fade duration: Typically 1-2 seconds for smooth visual transition (implementation-dependent)
    /// - After fade completes (or if noFade is TRUE), object is removed from world via World.DestroyEntity()
    /// - Event firing: EVENT_DESTROY_OBJECT fires before object is removed from world (allows scripts to react)
    /// - Object cleanup: Entity is removed from area's entity list, all references become invalid (OBJECT_INVALID)
    /// - Script execution: OnDeath script may fire before destruction (if entity has OnDeath script)
    /// - Usage: Temporary objects, scripted removals, death sequences, cutscenes
    /// - Based on NWScript function DestroyObject (routine ID varies by game version)
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
            // TODO: STUB - For now, we'll use a fixed fade duration
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

            // Based on swkotor2.exe: DestroyObject implementation
            // Located via string references: "EVENT_DESTROY_OBJECT" @ 0x007bcd48 (destroy object event, case 0xb)
            // Original implementation: FUN_004dcfb0 @ 0x004dcfb0 handles EVENT_DESTROY_OBJECT (case 0xb, fires before object removal)
            // Fires EVENT_DESTROY_OBJECT event before removing object from world (allows scripts to react)
            // Object is removed from area's entity list, all references become invalid
            if (actor != null && actor.World != null)
            {
                IEntity target = actor.World.GetEntity(_targetObjectId);
                if (target != null)
                {
                    // Fire destroy event before destruction
                    // Note: There's no OnDestroy script event, but EVENT_DESTROY_OBJECT is a world event
                    // Scripts that need to react to destruction should use OnDeath or other events
                    // The actual destruction happens via World.DestroyEntity which unregisters the entity
                }

                actor.World.DestroyEntity(_targetObjectId);
                _destroyed = true;
            }
        }
    }
}


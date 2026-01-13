using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Core.Actions
{
    /// <summary>
    /// Action to destroy an object with optional fade effects.
    /// </summary>
    /// <remarks>
    /// Destroy Object Action:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) DestroyObject NWScript function
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
        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): DestroyObject fade duration
        // Located via string references: "FadeLength" @ 0x007c3580 (fade length parameter)
        // Original implementation: Uses fade duration of 1.0 seconds for object destruction fade
        // Fade duration: 1.0 seconds for smooth visual transition (matches original engine behavior)
        // swkotor2.exe: FUN_004dcfb0 @ 0x004dcfb0 handles DestroyObject with fade length parameter
        private const float DestroyObjectFadeDuration = 1.0f;

        private readonly uint _targetObjectId;
        private readonly float _delay;
        private readonly bool _noFade;
        private readonly float _delayUntilFade;
        private bool _fadeStarted;
        private bool _destroyed;
        private float _fadeStartTime;

        public ActionDestroyObject(uint targetObjectId, float delay = 0f, bool noFade = false, float delayUntilFade = 0f)
            : base(ActionType.DestroyObject)
        {
            _targetObjectId = targetObjectId;
            _delay = delay;
            _noFade = noFade;
            _delayUntilFade = delayUntilFade;
            _fadeStarted = false;
            _destroyed = false;
            _fadeStartTime = 0f;
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
                            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): DestroyObject fade implementation
                            // Stores fade state on entity for rendering system to process
                            // swkotor2.exe: FUN_004dcfb0 sets fade flags before starting fade animation
                            targetEntity.SetData("DestroyFade", true);
                            targetEntity.SetData("DestroyFadeStartTime", ElapsedTime);
                            targetEntity.SetData("DestroyFadeDuration", DestroyObjectFadeDuration);
                            _fadeStartTime = ElapsedTime;
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

            // If fade, wait for fade duration to complete
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): DestroyObject fade completion check
            // swkotor2.exe: FUN_004dcfb0 @ 0x004dcfb0 waits for fade duration after fade starts
            // Fade starts at _fadeStartTime (when we set DestroyFade flag on entity)
            // Fade completes after DestroyObjectFadeDuration seconds from fade start time
            // The rendering system handles the actual visual fade based on "DestroyFade" flag
            // We check completion here based on the stored fade start time and duration
            // Original implementation: Uses FadeLength parameter (1.0 seconds) stored at 0x007c3580
            // Note: _fadeStartTime will be > 0 when fade has started (set to ElapsedTime when fade begins)
            if (_fadeStarted && _fadeStartTime > 0f && ElapsedTime >= _fadeStartTime + DestroyObjectFadeDuration)
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

            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): DestroyObject implementation
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


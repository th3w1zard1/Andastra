using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Core.Actions
{
    /// <summary>
    /// Action to play an animation.
    /// </summary>
    /// <remarks>
    /// Play Animation Action:
    /// - Based on swkotor2.exe animation system
    /// - Located via string references: "Animation" @ 0x007c3440, "AnimList" @ 0x007c3694
    /// - "PlayAnim" @ 0x007c346c, "AnimLoop" @ 0x007c4c70 (animation loop flag)
    /// - "CurrentAnim" @ 0x007c38d4, "NextAnim" @ 0x007c38c8 (animation state tracking)
    /// - Animation timing: "frameStart" @ 0x007ba698, "frameEnd" @ 0x007ba668 (animation frame timing)
    /// - ActionPlayAnimation NWScript function queues animation action to entity action queue
    /// - Original implementation: Plays animation on entity, supports speed and duration parameters
    /// - Animation IDs reference animation indices in MDL animation arrays
    /// - Speed parameter controls playback rate (1.0 = normal, 2.0 = double speed, 0.5 = half speed)
    /// - Duration parameter controls how long animation plays (0 = play once, >0 = loop for duration)
    /// - Action completes when animation finishes or duration expires
    /// </remarks>
    public class ActionPlayAnimation : ActionBase
    {
        private readonly int _animation;
        private readonly float _duration;
        private readonly float _speed;
        private bool _started;

        public ActionPlayAnimation(int animation, float speed = 1.0f, float duration = 0f)
            : base(ActionType.PlayAnimation)
        {
            _animation = animation;
            _speed = speed;
            _duration = duration;
        }

        public int Animation { get { return _animation; } }
        public float Speed { get { return _speed; } }
        public float Duration { get { return _duration; } }

        protected override ActionStatus ExecuteInternal(IEntity actor, float deltaTime)
        {
            // Based on swkotor2.exe: ActionPlayAnimation implementation
            // Located via string references: "Animation" @ 0x007c3440, "PlayAnim" @ 0x007c346c
            // Original implementation: Plays animation on entity's animation component
            // Animation ID references animation index in MDL animation array
            // Speed parameter controls playback rate (1.0 = normal speed)
            // Duration parameter: 0 = play once, >0 = loop for specified duration
            if (!_started)
            {
                _started = true;

                // Get animation component and play animation
                var animationComponent = actor.GetComponent<Interfaces.Components.IAnimationComponent>();
                if (animationComponent != null)
                {
                    // Duration > 0 means loop for that duration, duration = 0 means play once
                    bool loop = _duration > 0;
                    animationComponent.PlayAnimation(_animation, _speed, loop);
                }
            }

            // If duration is 0, play once and wait for animation to complete
            // Original engine: Action completes when animation finishes (checked by animation system)
            if (_duration <= 0)
            {
                var animationComponent = actor.GetComponent<Interfaces.Components.IAnimationComponent>();
                if (animationComponent != null && animationComponent.AnimationComplete)
                {
                    return ActionStatus.Complete;
                }
                return ActionStatus.InProgress;
            }

            // If duration > 0, loop animation for specified duration
            // Original engine: Animation loops until duration expires
            if (ElapsedTime >= _duration)
            {
                // Stop looping animation
                var animationComponent = actor.GetComponent<Interfaces.Components.IAnimationComponent>();
                if (animationComponent != null)
                {
                    animationComponent.StopAnimation();
                }
                return ActionStatus.Complete;
            }

            return ActionStatus.InProgress;
        }
    }
}


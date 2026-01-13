using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Core.Actions
{
    /// <summary>
    /// Action to speak a string (bark bubble).
    /// </summary>
    /// <remarks>
    /// Speak String Action:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) speech/bark system
    /// - Located via string references: "TalkString" @ 0x007c14f8, "Speaker" @ 0x007c35f8
    /// - "SpeakString" NWScript function, "ActionSpeakString" action type
    /// - Talk volume enumeration: 0=Normal, 1=Whisper, 2=Shout, 3=Party, 4=Silent
    /// - VO (voice over) references: "VO" folder, TLK (talk table) string references
    /// - Original implementation: Displays text bubble above entity, plays voice if available, lip sync animation
    /// - Speech bubbles displayed above speaking entity for specified duration (typically 3-5 seconds based on text length)
    /// - Can play voice over (VO) audio if voice file exists matching dialogue string reference
    /// - VO files stored in VO folder, referenced by string ID from TLK (talk table) files
    /// - Original engine: SpeakString function queues action, displays speech bubble, plays VO if available
    /// - Action completion: Action completes immediately after queuing speech display (async UI display)
    /// </remarks>
    public class ActionSpeakString : ActionBase
    {
        private readonly string _text;
        private readonly int _talkVolume;

        /// <summary>
        /// Creates a new speak string action.
        /// </summary>
        /// <param name="text">The text to speak.</param>
        /// <param name="talkVolume">0=Normal, 1=Whisper, 2=Shout, 3=Party, 4=Silent</param>
        public ActionSpeakString(string text, int talkVolume = 0)
            : base(ActionType.SpeakString)
        {
            _text = text ?? string.Empty;
            _talkVolume = talkVolume;
        }

        public string Text { get { return _text; } }
        public int TalkVolume { get { return _talkVolume; } }

        protected override ActionStatus ExecuteInternal(IEntity actor, float deltaTime)
        {
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): ActionSpeakString implementation
            // Located via string references: "SpeakString" NWScript function, "TalkString" @ 0x007c14f8
            // Original implementation: Action completes immediately after queuing speech display
            // The actual UI display (speech bubble) is handled by the game layer asynchronously
            // Voice over (VO) playback and lip sync are also handled by game layer
            // Action doesn't wait for speech bubble to disappear - it completes immediately
            return ActionStatus.Complete;
        }
    }
}


using System;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Core.Dialogue
{
    /// <summary>
    /// Interface for loading dialogue files.
    /// </summary>
    /// <remarks>
    /// Dialogue Loader Interface:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) dialogue system
    /// - Located via string references: "Conversation" @ 0x007c1abc, "ConversationType" @ 0x007c38b0, "EndConversation" @ 0x007c38e0
    /// - "Conversation File: " @ 0x007cb1ac
    /// - Dialogue script hooks: "ScriptDialogue" @ 0x007bee40, "ScriptEndDialogue" @ 0x007bede0
    /// - Dialogue events: "CSWSSCRIPTEVENT_EVENTTYPE_ON_DIALOGUE" @ 0x007bcac4, "OnEndDialogue" @ 0x007c1f60
    /// - Example dialogue script: "k_hen_dialogue01" @ 0x007bf548
    /// - Error: "CONVERSATION ERROR: Last Conversation Node Contains Either an END NODE or CONTINUE NODE.  Please contact a Designer!" @ 0x007c3768
    /// - Error: "Error: dialogue can't find object '%s'!" @ 0x007c3730 (dialogue object lookup failure)
    /// - Original implementation: Loads DLG (dialogue) files from resource system
    /// - DLG file format: GFF with "DLG " signature containing dialogue tree data
    /// - Dialogue files contain entries (NPC lines), replies (player options), and links between them
    /// - BeginConversation NWScript function starts dialogue with DLG ResRef
    /// - Based on DLG file format documentation in vendor/PyKotor/wiki/
    /// </remarks>
    public interface IDialogueLoader
    {
        /// <summary>
        /// Loads a dialogue from resource reference.
        /// </summary>
        /// <param name="resRef">The dialogue resource reference.</param>
        /// <returns>The loaded dialogue, or null if not found.</returns>
        RuntimeDialogue LoadDialogue(string resRef);
    }

    /// <summary>
    /// Interface for executing NWScript scripts.
    /// </summary>
    /// <remarks>
    /// Script Executor Interface:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) NCS VM system
    /// - Located via string references: NCS VM execution functions
    /// - Original implementation: Executes compiled NWScript (NCS) files via NCS VM
    /// - Script execution: Loads NCS file, creates execution context, runs VM until completion
    /// - Return value: 0 = FALSE, non-zero = TRUE (used for conditional checks in dialogue, etc.)
    /// - Script context: OBJECT_SELF (owner), OBJECT_INVALID (triggerer), global/local variables
    /// </remarks>
    public interface IScriptExecutor
    {
        /// <summary>
        /// Executes a script.
        /// </summary>
        /// <param name="scriptResRef">The script resource reference.</param>
        /// <param name="owner">The owner entity (OBJECT_SELF).</param>
        /// <param name="triggerer">The triggering entity.</param>
        /// <returns>The script return value (0 = FALSE, non-zero = TRUE).</returns>
        int ExecuteScript(string scriptResRef, IEntity owner, IEntity triggerer);
    }

    /// <summary>
    /// Interface for playing voice-over audio.
    /// </summary>
    /// <remarks>
    /// Voice Player Interface:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) voice-over system
    /// - Located via string references: Voice-over playback functions
    /// - Original implementation: Plays voice-over audio files (WAV format) during dialogue
    /// - Voice-over files: Stored in VO folder, referenced by dialogue entries (VoiceOverId)
    /// - Positional audio: Speaker entity position used for 3D audio positioning
    /// - Playback completion: Callback fires when voice-over finishes (for dialogue progression)
    /// </remarks>
    public interface IVoicePlayer
    {
        /// <summary>
        /// Plays a voice-over.
        /// </summary>
        /// <param name="voResRef">The voice-over resource reference.</param>
        /// <param name="speaker">The speaking entity (for positional audio).</param>
        /// <param name="onComplete">Callback when playback completes.</param>
        void Play(string voResRef, IEntity speaker, Action onComplete);

        /// <summary>
        /// Stops the currently playing voice-over.
        /// </summary>
        void Stop();

        /// <summary>
        /// Gets whether voice-over is currently playing.
        /// </summary>
        bool IsPlaying { get; }

        /// <summary>
        /// Gets the current playback position in seconds.
        /// </summary>
        float CurrentTime { get; }

        /// <summary>
        /// Gets or sets the voice volume (0.0 to 1.0).
        /// Based on swkotor.exe and swkotor2.exe: VoiceVolume setting from INI file
        /// </summary>
        float Volume { get; set; }
    }

    /// <summary>
    /// Interface for controlling lip sync animation.
    /// </summary>
    public interface ILipSyncController
    {
        /// <summary>
        /// Starts lip sync for a speaker.
        /// </summary>
        /// <param name="speaker">The speaking entity.</param>
        /// <param name="lipResRef">The LIP file resource reference.</param>
        void Start(IEntity speaker, string lipResRef);

        /// <summary>
        /// Stops lip sync animation.
        /// </summary>
        void Stop();

        /// <summary>
        /// Updates lip sync animation.
        /// </summary>
        /// <param name="deltaTime">Time since last frame in seconds.</param>
        void Update(float deltaTime);

        /// <summary>
        /// Gets whether lip sync is currently active.
        /// </summary>
        bool IsActive { get; }
    }

    /// <summary>
    /// Interface for camera control during dialogue.
    /// </summary>
    public interface IDialogueCameraController
    {
        /// <summary>
        /// Sets the camera to focus on the speaker and listener.
        /// </summary>
        /// <param name="speaker">The speaking entity.</param>
        /// <param name="listener">The listening entity.</param>
        void SetFocus(IEntity speaker, IEntity listener);

        /// <summary>
        /// Sets the camera angle.
        /// </summary>
        /// <param name="angle">The camera angle index.</param>
        void SetAngle(int angle);

        /// <summary>
        /// Sets the camera animation.
        /// </summary>
        /// <param name="animId">The camera animation ID.</param>
        void SetAnimation(int animId);

        /// <summary>
        /// Resets the camera to normal gameplay mode.
        /// </summary>
        void Reset();
    }
}

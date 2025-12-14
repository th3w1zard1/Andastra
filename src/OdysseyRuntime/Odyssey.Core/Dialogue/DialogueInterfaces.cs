using System;
using Odyssey.Core.Interfaces;

namespace Odyssey.Core.Dialogue
{
    /// <summary>
    /// Interface for loading dialogue files.
    /// </summary>
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

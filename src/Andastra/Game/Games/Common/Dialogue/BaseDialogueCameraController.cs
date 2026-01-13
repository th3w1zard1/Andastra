using System;
using Andastra.Runtime.Core.Camera;
using Andastra.Runtime.Core.Dialogue;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Game.Games.Common.Dialogue
{
    /// <summary>
    /// Abstract base class for dialogue camera controllers across all engines.
    /// </summary>
    /// <remarks>
    /// Base Dialogue Camera Controller:
    /// - Common functionality shared across all BioWare engines (Odyssey, Aurora, Eclipse, Infinity)
    /// - Base classes MUST only contain functionality that is identical across ALL engines
    /// - Engine-specific details MUST be in subclasses (OdysseyDialogueCameraController, AuroraDialogueCameraController, etc.)
    /// - Common: Interface contract, basic camera controller reference, speaker/listener tracking
    /// - Engine-specific: Reset() behavior, camera angle/animation systems, player entity lookup strategies
    /// </remarks>
    public abstract class BaseDialogueCameraController : IDialogueCameraController
    {
        /// <summary>
        /// The camera controller used for actual camera control.
        /// </summary>
        protected readonly CameraController CameraController;

        /// <summary>
        /// Current speaker entity.
        /// </summary>
        protected IEntity CurrentSpeaker;

        /// <summary>
        /// Current listener entity.
        /// </summary>
        protected IEntity CurrentListener;

        /// <summary>
        /// Initializes a new dialogue camera controller.
        /// </summary>
        /// <param name="cameraController">The camera controller to use.</param>
        protected BaseDialogueCameraController(CameraController cameraController)
        {
            if (cameraController == null)
            {
                throw new ArgumentNullException(nameof(cameraController));
            }

            CameraController = cameraController;
        }

        /// <summary>
        /// Sets the camera to focus on the speaker and listener.
        /// </summary>
        /// <param name="speaker">The speaking entity.</param>
        /// <param name="listener">The listening entity.</param>
        public abstract void SetFocus(IEntity speaker, IEntity listener);

        /// <summary>
        /// Sets the camera angle.
        /// </summary>
        /// <param name="angle">The camera angle index.</param>
        public abstract void SetAngle(int angle);

        /// <summary>
        /// Sets the camera animation.
        /// </summary>
        /// <param name="animId">The camera animation ID.</param>
        public abstract void SetAnimation(int animId);

        /// <summary>
        /// Resets the camera to normal gameplay mode.
        /// Engine-specific implementation required.
        /// </summary>
        public abstract void Reset();
    }
}


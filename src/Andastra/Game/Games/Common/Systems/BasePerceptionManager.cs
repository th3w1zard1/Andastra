using System;
using System.Collections.Generic;
using Andastra.Runtime.Core.Interfaces;
using JetBrains.Annotations;

namespace Andastra.Runtime.Games.Common.Systems
{
    /// <summary>
    /// Abstract base class for perception managers across all engines.
    /// </summary>
    /// <remarks>
    /// Base Perception Manager:
    /// - Common functionality shared across all BioWare engines (Odyssey, Aurora, Eclipse, Infinity)
    /// - Base classes MUST only contain functionality that is identical across ALL engines
    /// - Engine-specific details MUST be in subclasses (OdysseyPerceptionManager, AuroraPerceptionManager, EclipsePerceptionManager)
    /// - Common: Perception tracking, sight/hearing detection, perception events
    /// </remarks>
    public abstract class BasePerceptionManager
    {
        protected readonly IWorld _world;

        /// <summary>
        /// Event fired when perception state changes.
        /// </summary>
        public event EventHandler<PerceptionEventArgs> OnPerceptionChanged;

        protected BasePerceptionManager(IWorld world)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            _world = world;
        }

        /// <summary>
        /// Updates perception for all entities (call each frame or periodically).
        /// </summary>
        public abstract void Update(float deltaTime);

        /// <summary>
        /// Registers an entity for perception tracking.
        /// </summary>
        public abstract void RegisterEntity(IEntity entity);

        /// <summary>
        /// Unregisters an entity from perception tracking.
        /// </summary>
        public abstract void UnregisterEntity(IEntity entity);

        /// <summary>
        /// Checks if perceiver can see target.
        /// </summary>
        public abstract bool CanSee(IEntity perceiver, IEntity target);

        /// <summary>
        /// Checks if perceiver can hear target.
        /// </summary>
        public abstract bool CanHear(IEntity perceiver, IEntity target);

        /// <summary>
        /// Fires the OnPerceptionChanged event.
        /// </summary>
        protected void FireOnPerceptionChanged(PerceptionEventArgs args)
        {
            OnPerceptionChanged?.Invoke(this, args);
        }
    }

    /// <summary>
    /// Perception event arguments (common across all engines).
    /// </summary>
    public class PerceptionEventArgs : EventArgs
    {
        public IEntity Perceiver { get; set; }
        public IEntity Perceived { get; set; }
        public int EventType { get; set; } // Engine-specific enum as int
    }
}


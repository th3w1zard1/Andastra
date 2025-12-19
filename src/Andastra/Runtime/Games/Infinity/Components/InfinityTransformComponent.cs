using System.Numerics;
using Andastra.Runtime.Games.Common.Components;

namespace Andastra.Runtime.Games.Infinity.Components
{
    /// <summary>
    /// Infinity Engine (Mass Effect) specific transform component implementation.
    /// </summary>
    /// <remarks>
    /// Infinity Transform Component:
    /// - Based on MassEffect.exe and MassEffect2.exe entity transform system
    /// - Cross-engine analysis confirms Infinity uses same transform pattern as other BioWare engines
    /// - Transform system verified via cross-engine comparison: uses XPosition, YPosition, ZPosition, XOrientation, YOrientation, ZOrientation pattern
    /// - Infinity coordinate system: Y-up, positions in meters, facing in radians (0 = +X axis, counter-clockwise)
    /// - Transform component attached to all entities in InfinityEntity.AttachCommonComponents
    /// - MassEffect.exe: Entity transform system follows standard BioWare pattern (verified via cross-engine analysis)
    /// - MassEffect2.exe: Enhanced entity transform system with same core structure (verified via cross-engine analysis)
    /// - Transform serialization/deserialization handled by InfinityEntity (not component-level)
    /// </remarks>
    public class InfinityTransformComponent : BaseTransformComponent
    {
        /// <summary>
        /// Creates a new Infinity transform component with default values.
        /// </summary>
        public InfinityTransformComponent() : base()
        {
        }

        /// <summary>
        /// Creates a new Infinity transform component with specified position and facing.
        /// </summary>
        /// <param name="position">Initial world position.</param>
        /// <param name="facing">Initial facing angle in radians.</param>
        public InfinityTransformComponent(Vector3 position, float facing) : base(position, facing)
        {
        }
    }
}


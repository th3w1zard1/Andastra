using System.Numerics;
using Andastra.Game.Games.Common.Components;

namespace Andastra.Game.Games.Eclipse.Components
{
    /// <summary>
    /// Eclipse Engine (Dragon Age) specific transform component implementation.
    /// </summary>
    /// <remarks>
    /// Eclipse Transform Component:
    /// - Based on daorigins.exe and DragonAge2.exe entity transform system
    /// - daorigins.exe: XPosition @ 0x00af4f68, YPosition @ 0x00af4f5c, ZPosition @ 0x00af4f50, XOrientation @ 0x00af4f40, YOrientation @ 0x00af4f30, ZOrientation @ 0x00af4f20
    /// - DragonAge2.exe: Similar transform system (verified via cross-engine analysis)
    /// - Eclipse coordinate system: Y-up, positions in meters, facing in radians (0 = +X axis, counter-clockwise)
    /// - Transform serialization/deserialization handled by EclipseEntity (not component-level)
    /// - Eclipse coordinate system: Y-up, positions in meters, facing in radians (0 = +X axis, counter-clockwise)
    /// - Transform component attached to all entities in EclipseEntity.AttachCommonComponents
    /// </remarks>
    public class EclipseTransformComponent : BaseTransformComponent
    {
        /// <summary>
        /// Creates a new Eclipse transform component with default values.
        /// </summary>
        public EclipseTransformComponent() : base()
        {
        }

        /// <summary>
        /// Creates a new Eclipse transform component with specified position and facing.
        /// </summary>
        /// <param name="position">Initial world position.</param>
        /// <param name="facing">Initial facing angle in radians.</param>
        public EclipseTransformComponent(Vector3 position, float facing) : base(position, facing)
        {
        }
    }
}


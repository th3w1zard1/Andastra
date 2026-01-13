using System.Numerics;
using Andastra.Game.Games.Common.Components;

namespace Andastra.Game.Games.Aurora.Components
{
    /// <summary>
    /// Aurora Engine (Neverwinter Nights) specific transform component implementation.
    /// </summary>
    /// <remarks>
    /// Aurora Transform Component:
    /// - Based on nwmain.exe entity transform system
    /// - Located via string references: XPosition @ 0x140ddb700, YPosition @ 0x140ddb710, ZPosition @ 0x140ddb720
    /// - XOrientation @ 0x140ddb750, YOrientation @ 0x140ddb740, ZOrientation @ 0x140ddb730
    /// - SaveCreature @ 0x1403a0a60 saves position/orientation to GFF
    /// - LoadCreatures @ 0x140360570 loads position/orientation from GFF
    /// - CNWSObject::SetOrientation used to set orientation from vector
    /// - Position stored in GFF structures as XPosition, YPosition, ZPosition
    /// - Orientation stored in GFF structures as XOrientation, YOrientation, ZOrientation
    /// - Aurora coordinate system: Y-up, positions in meters, facing in radians (0 = +X axis, counter-clockwise)
    /// - Transform component attached to all entities in AuroraEntity.AttachCommonComponents
    /// - Engine-specific transform component classes have been merged into BaseTransformComponent
    /// </remarks>
    public class AuroraTransformComponent : BaseTransformComponent
    {
        /// <summary>
        /// Creates a new Aurora transform component with default values.
        /// </summary>
        public AuroraTransformComponent() : base()
        {
        }

        /// <summary>
        /// Creates a new Aurora transform component with specified position and facing.
        /// </summary>
        /// <param name="position">Initial world position.</param>
        /// <param name="facing">Initial facing angle in radians.</param>
        public AuroraTransformComponent(Vector3 position, float facing) : base(position, facing)
        {
        }
    }
}


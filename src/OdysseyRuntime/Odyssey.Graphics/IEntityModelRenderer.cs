using System.Numerics;
using JetBrains.Annotations;
using Odyssey.Core.Interfaces;

namespace Odyssey.Graphics
{
    /// <summary>
    /// Entity model renderer abstraction for rendering entity models.
    /// </summary>
    public interface IEntityModelRenderer
    {
        /// <summary>
        /// Renders an entity using its model.
        /// </summary>
        /// <param name="entity">Entity to render.</param>
        /// <param name="viewMatrix">View transformation matrix.</param>
        /// <param name="projectionMatrix">Projection transformation matrix.</param>
        void RenderEntity([NotNull] IEntity entity, Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix);
    }
}


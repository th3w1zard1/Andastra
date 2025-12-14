using System.Numerics;
using Odyssey.Core.Enums;

namespace Odyssey.Core.Interfaces
{
    /// <summary>
    /// Template for creating entities (loaded from GFF templates like UTC, UTP, UTD, etc.)
    /// </summary>
    public interface IEntityTemplate
    {
        /// <summary>
        /// The resource reference of this template.
        /// </summary>
        string ResRef { get; }

        /// <summary>
        /// The tag to assign to spawned entities.
        /// </summary>
        string Tag { get; }

        /// <summary>
        /// The object type this template creates.
        /// </summary>
        ObjectType ObjectType { get; }

        /// <summary>
        /// Spawns an entity from this template.
        /// </summary>
        IEntity Spawn(IWorld world, Vector3 position, float facing);
    }
}


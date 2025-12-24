using System.Numerics;
using Andastra.Runtime.Core.Interfaces;
using JetBrains.Annotations;

namespace Andastra.Runtime.Graphics
{
    /// <summary>
    /// Entity model renderer abstraction for rendering entity models.
    /// </summary>
    /// <remarks>
    /// Entity Model Renderer Interface:
    /// - Based on swkotor2.exe entity model rendering system
    /// - Located via string references: "ModelName" @ 0x007c1c8c, "Model" @ 0x007c1ca8, "VisibleModel" @ 0x007c1c98
    /// - "ModelType" @ 0x007c4568, "MODELTYPE" @ 0x007c036c, "ModelVariation" @ 0x007c0990
    /// - "ModelPart" @ 0x007bd42c, "ModelPart1" @ 0x007c0acc, "ModelA" @ 0x007bf4bc
    /// - "DefaultModel" @ 0x007c4530, "StuntModel" @ 0x007c37e0, "CameraModel" @ 0x007c3908, "ProjModel" @ 0x007c31c0
    /// - "refModel" @ 0x007babe8, "c_FocusGobDummyModel%d" @ 0x007b985c
    /// - CSWCCreature::LoadModel @ 0x007c82fc (creature model loading), FUN_005261b0 @ 0x005261b0 (model loading function)
    /// - Original implementation: Renders entity models (creatures, placeables, items) from MDL/MDX files
    /// - Model loading: Loads MDL (model) and MDX (animation) files, applies textures, renders with transformations
    /// - This interface: Abstraction layer for modern graphics backends (MonoGame, Stride)
    /// </remarks>
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


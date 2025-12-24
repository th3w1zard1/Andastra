using System;
using System.Numerics;
using Andastra.Parsing.Installation;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Graphics;
using Andastra.Runtime.MonoGame.Converters;
using Andastra.Runtime.MonoGame.Rendering;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Runtime.MonoGame.Graphics
{
    /// <summary>
    /// MonoGame implementation of IEntityModelRenderer.
    /// </summary>
    public class MonoGameEntityModelRenderer : IEntityModelRenderer
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly Andastra.Runtime.MonoGame.Rendering.EntityModelRenderer _renderer;

        public MonoGameEntityModelRenderer(
            [NotNull] GraphicsDevice device,
            object gameDataManager = null,
            object installation = null)
        {
            if (device == null)
            {
                throw new ArgumentNullException(nameof(device));
            }

            _graphicsDevice = device;

            // Create the underlying MonoGame renderer if dependencies are provided
            // EntityModelRenderer requires both GameDataManager and Installation for model loading
            // GameDataManager provides access to 2DA tables (appearance.2da, placeables.2da, etc.) for model resolution
            // Installation provides access to resource system (MDL files, textures, etc.)
            // Based on swkotor2.exe: FUN_005261b0 @ 0x005261b0 loads creature model from appearance.2da
            // Note: Using object types to avoid circular dependency - EntityModelRenderer accepts object for gameDataManager
            if (gameDataManager != null && installation != null)
            {
                Installation inst = installation as Installation;

                if (inst != null)
                {
                    _renderer = new EntityModelRenderer(device, gameDataManager, inst);
                }
            }
        }

        public void RenderEntity([NotNull] IEntity entity, Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            if (_renderer == null)
            {
                // Cannot render without dependencies
                return;
            }

            // Convert matrices to MonoGame format
            var mgView = ConvertMatrix(viewMatrix);
            var mgProjection = ConvertMatrix(projectionMatrix);

            _renderer.RenderEntity(entity, mgView, mgProjection);
        }

        private static Microsoft.Xna.Framework.Matrix ConvertMatrix(Matrix4x4 matrix)
        {
            return new Microsoft.Xna.Framework.Matrix(
                matrix.M11, matrix.M12, matrix.M13, matrix.M14,
                matrix.M21, matrix.M22, matrix.M23, matrix.M24,
                matrix.M31, matrix.M32, matrix.M33, matrix.M34,
                matrix.M41, matrix.M42, matrix.M43, matrix.M44
            );
        }
    }
}


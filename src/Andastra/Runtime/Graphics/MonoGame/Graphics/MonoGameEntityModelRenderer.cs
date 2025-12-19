using System;
using System.Numerics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Graphics;
using Andastra.Runtime.MonoGame.Converters;
using JetBrains.Annotations;

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
            if (gameDataManager != null && installation != null)
            {
                _renderer = new Odyssey.MonoGame.Rendering.EntityModelRenderer(
                    device,
                    gameDataManager as Andastra.Runtime.Engines.Odyssey.Data.GameDataManager,
                    installation as Andastra.Parsing.Installation.Installation
                );
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


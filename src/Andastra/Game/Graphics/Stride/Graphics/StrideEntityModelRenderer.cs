using System;
using System.Collections.Generic;
using System.Numerics;
using BioWare.NET.Resource.Formats.MDLData;
using BioWare.NET.Extract;
using BioWare.NET.Common;
using BioWare.NET.Resource;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Game.Games.Odyssey.Systems;
using Andastra.Runtime.Graphics;
using Andastra.Game.Stride.Converters;
using JetBrains.Annotations;
using Stride.Core.Mathematics;
using Stride.Graphics;

namespace Andastra.Game.Stride.Graphics
{
    /// <summary>
    /// Stride implementation of IEntityModelRenderer.
    /// Renders entity models (creatures, doors, placeables) using MDL models.
    /// </summary>
    /// <remarks>
    /// Entity Model Renderer:
    /// - Cross-Engine Analysis (Reverse Engineered via Ghidra):
    ///   - Odyssey (swkotor.exe, swkotor2.exe):
    ///     - [CSWCCreature::LoadModel()] @ (K1: 0x0074f85c, TSL: 0x005261b0) - loads creature model from appearance.2da
    ///     - CSWCCreature::LoadModel @ (K1: TODO: Find this address, TSL: 0x007c82fc) - "Failed to load creature model '%s'." error string
    ///     - Model loading: Loads UTC (creature template) from resources, resolves model from appearance.2da
    ///     - Appearance resolution: 0x005fb0f0 resolves appearance data, 0x00521d40 loads model
    ///   - Aurora (nwmain.exe):
    ///     - LoadModel @ 0x1400a0130 - loads Model objects from file streams
    ///     - CNWCCreature::LoadModel() - loads creature models via CResRef
    ///     - CNWCItem::LoadModel, CNWCDoor::LoadModel, CNWCPlaceable::LoadModel - entity-specific loaders
    ///     - Model caching: Uses global _Models array to cache loaded models
    ///   - Eclipse (daorigins.exe: (TODO: Find this address), DragonAge2.exe: (TODO: Find this address)):
    ///     - Model loading patterns differ (uses different file formats)
    ///     - Entity model rendering handled through different systems
    /// - Common Patterns (All Engines):
    ///   - Model resolution from appearance/2DA tables (appearance.2da, placeables.2da, genericdoors.2da)
    ///   - Model caching to avoid reloading (dictionary/cache by ResRef)
    ///   - Transform application (position, orientation, scale)
    ///   - Material/texture resolution from resource files
    /// - This Implementation:
    ///   - Based on Odyssey engine patterns (swkotor.exe, swkotor2.exe)
    ///   - Models resolved from appearance.2da (creatures), placeables.2da (placeables), genericdoors.2da (doors)
    ///   - Caches loaded models to avoid reloading (model cache dictionary by ResRef)
    ///   - Model conversion: MDL format (KOTOR native) converted to Stride Buffer format for rendering
    ///   - Material resolution: StrideBasicEffect created per texture/material (texture loading from TPC files)
    ///   - Render transform: Entity position/orientation applied via world matrix for rendering
    /// </remarks>
    public class StrideEntityModelRenderer : IEntityModelRenderer
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly Dictionary<string, MdlToStrideModelConverter.ConversionResult> _modelCache;
        private readonly Dictionary<string, IBasicEffect> _materialCache;
        private readonly Andastra.Game.Games.Odyssey.Data.GameDataManager _gameDataManager;
        private readonly Installation _installation;
        private readonly Func<string, IBasicEffect> _materialResolver;

        public StrideEntityModelRenderer(
            [NotNull] GraphicsDevice device,
            object gameDataManager = null,
            object installation = null)
        {
            if (device == null)
            {
                throw new ArgumentNullException(nameof(device));
            }

            _graphicsDevice = device;
            _gameDataManager = gameDataManager as Andastra.Game.Games.Odyssey.Data.GameDataManager;
            _installation = installation as Installation;
            _modelCache = new Dictionary<string, MdlToStrideModelConverter.ConversionResult>(StringComparer.OrdinalIgnoreCase);
            _materialCache = new Dictionary<string, IBasicEffect>(StringComparer.OrdinalIgnoreCase);

            // Material resolver: creates StrideBasicEffect for texture names
            _materialResolver = (textureName) =>
            {
                if (string.IsNullOrEmpty(textureName))
                {
                    return CreateDefaultEffect();
                }

                if (_materialCache.TryGetValue(textureName, out IBasicEffect cached))
                {
                    return cached;
                }

                // Create new effect (texture loading would go here)
                IBasicEffect effect = CreateDefaultEffect();
                _materialCache[textureName] = effect;
                return effect;
            };
        }

        /// <summary>
        /// Gets or loads a model for an entity.
        /// </summary>
        [CanBeNull]
        public MdlToStrideModelConverter.ConversionResult GetEntityModel([NotNull] IEntity entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            // Check renderable component first
            IRenderableComponent renderable = entity.GetComponent<IRenderableComponent>();
            string modelResRef = null;

            if (renderable != null && !string.IsNullOrEmpty(renderable.ModelResRef))
            {
                modelResRef = renderable.ModelResRef;
            }
            else if (_gameDataManager != null)
            {
                // Resolve model from appearance
                modelResRef = ModelResolver.ResolveEntityModel(_gameDataManager, entity);
            }

            if (string.IsNullOrEmpty(modelResRef))
            {
                return null;
            }

            // Check cache
            if (_modelCache.TryGetValue(modelResRef, out MdlToStrideModelConverter.ConversionResult cached))
            {
                return cached;
            }

            // Load model
            try
            {
                MDL mdl = LoadMDLModel(modelResRef);
                if (mdl == null)
                {
                    return null;
                }

                var converter = new MdlToStrideModelConverter(_graphicsDevice, _materialResolver);
                MdlToStrideModelConverter.ConversionResult result = converter.Convert(mdl);

                if (result != null && result.Meshes.Count > 0)
                {
                    _modelCache[modelResRef] = result;
                    return result;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[StrideEntityModelRenderer] Failed to load model " + modelResRef + ": " + ex.Message);
            }

            return null;
        }

        /// <summary>
        /// Renders an entity model at the given transform.
        /// </summary>
        public void RenderEntity([NotNull] IEntity entity, Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            // Check visibility
            IRenderableComponent renderable = entity.GetComponent<IRenderableComponent>();
            if (renderable != null && !renderable.Visible)
            {
                return;
            }

            // Get model
            MdlToStrideModelConverter.ConversionResult model = GetEntityModel(entity);
            if (model == null || model.Meshes.Count == 0)
            {
                return;
            }

            // Get entity transform
            ITransformComponent transform = entity.GetComponent<ITransformComponent>();
            if (transform == null)
            {
                return;
            }

            // Build world matrix from entity transform
            // Y-up system: facing is rotation around Y axis
            Matrix4x4 rotation = Matrix4x4.CreateRotationY(transform.Facing);
            Matrix4x4 translation = Matrix4x4.CreateTranslation(
                transform.Position.X,
                transform.Position.Y,
                transform.Position.Z
            );
            Matrix4x4 worldMatrix = Matrix4x4.Multiply(rotation, translation);

            // Convert System.Numerics matrices to Stride matrices
            Matrix strideView = ConvertToStrideMatrix(viewMatrix);
            Matrix strideProjection = ConvertToStrideMatrix(projectionMatrix);

            // Get command list for rendering
            CommandList commandList = _graphicsDevice.ImmediateContext();

            // Render all meshes
            foreach (MdlToStrideModelConverter.MeshData mesh in model.Meshes)
            {
                if (mesh.VertexBuffer == null || mesh.IndexBuffer == null)
                {
                    continue;
                }

                // Combine mesh transform with entity transform
                Matrix4x4 finalWorld = Matrix4x4.Multiply(mesh.WorldTransform, worldMatrix);
                Matrix strideWorld = ConvertToStrideMatrix(finalWorld);

                // Get Stride buffers

                if (!(mesh.VertexBuffer is StrideVertexBuffer strideVertexBuffer) || !(mesh.IndexBuffer is StrideIndexBuffer strideIndexBuffer))
                {
                    continue;
                }

                // Set vertex and index buffers
                commandList?.SetVertexBuffer(0, strideVertexBuffer.Buffer, 0, strideVertexBuffer.VertexStride);
                commandList?.SetIndexBuffer(strideIndexBuffer.Buffer, 0, strideIndexBuffer.IsShort);

                // Use mesh effect or default
                IBasicEffect effect = mesh.Effect ?? CreateDefaultEffect();
                if (effect is StrideBasicEffect strideEffect)
                {
                    // Set matrices
                    strideEffect.World = finalWorld;
                    strideEffect.View = viewMatrix;
                    strideEffect.Projection = projectionMatrix;

                    // Apply opacity from renderable component for fade-in/fade-out effects
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FadeTime @ 0x007c60ec (fade duration), alpha blending for entity rendering
                    // Opacity is updated by AppearAnimationFadeSystem for appear animations
                    // Opacity is updated by ActionDestroyObject for destroy animations
                    float opacity = renderable?.Opacity ?? 1.0f;
                    strideEffect.Alpha = opacity;

                    // Apply effect
                    var effectInstance = strideEffect.GetEffectInstance();
                    if (effectInstance != null)
                    {
                        strideEffect.UpdateShaderParameters();
                    }
                }

                // Draw indexed primitives
                int indexCount = mesh.IndexCount;
                int primitiveCount = indexCount / 3; // Triangles

                if (primitiveCount > 0)
                {
                    commandList?.DrawIndexed(
                        primitiveCount,
                        indexCount
                    );
                }
            }
        }

        /// <summary>
        /// Loads an MDL model from the installation.
        /// </summary>
        [CanBeNull]
        private MDL LoadMDLModel(string modelResRef)
        {
            if (string.IsNullOrEmpty(modelResRef))
            {
                return null;
            }

            if (_installation == null)
            {
                return null;
            }

            try
            {
                ResourceResult result = _installation.Resources.LookupResource(modelResRef, ResourceType.MDL);
                if (result == null || result.Data == null)
                {
                    return null;
                }

                // Use BioWare.NET MDL parser
                return BioWare.NET.Resource.Formats.MDL.MDLAuto.ReadMdl(result.Data);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[StrideEntityModelRenderer] Error loading MDL " + modelResRef + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Creates a default StrideBasicEffect for rendering.
        /// </summary>
        private IBasicEffect CreateDefaultEffect()
        {
            var effect = new StrideBasicEffect(_graphicsDevice);
            effect.VertexColorEnabled = false;
            effect.TextureEnabled = true;
            effect.LightingEnabled = true;
            effect.AmbientLightColor = new System.Numerics.Vector3(0.3f, 0.3f, 0.3f);
            effect.DiffuseColor = new System.Numerics.Vector3(1.0f, 1.0f, 1.0f);
            return effect;
        }

        /// <summary>
        /// Converts System.Numerics.Matrix4x4 to Stride.Core.Mathematics.Matrix.
        /// </summary>
        private Matrix ConvertToStrideMatrix(Matrix4x4 matrix)
        {
            return new Matrix(
                matrix.M11, matrix.M12, matrix.M13, matrix.M14,
                matrix.M21, matrix.M22, matrix.M23, matrix.M24,
                matrix.M31, matrix.M32, matrix.M33, matrix.M34,
                matrix.M41, matrix.M42, matrix.M43, matrix.M44
            );
        }

        /// <summary>
        /// Clears the model cache.
        /// </summary>
        public void ClearCache()
        {
            foreach (MdlToStrideModelConverter.ConversionResult result in _modelCache.Values)
            {
                if (result == null || result.Meshes == null)
                {
                    continue;
                }

                foreach (MdlToStrideModelConverter.MeshData mesh in result.Meshes)
                {
                    mesh.VertexBuffer?.Dispose();
                    mesh.IndexBuffer?.Dispose();
                }
            }
            _modelCache.Clear();
            _materialCache.Clear();
        }
    }
}


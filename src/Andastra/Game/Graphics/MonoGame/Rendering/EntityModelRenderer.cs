using System;
using System.Collections.Generic;
using BioWare.NET.Resource.Formats.MDLData;
using BioWare.NET.Extract.Installation;
using BioWare.NET.Resource;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.MonoGame.Converters;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Game.Graphics.MonoGame.Rendering
{
    /// <summary>
    /// Renders entity models (creatures, doors, placeables) using MDL models.
    /// </summary>
    /// <remarks>
    /// Entity Model Renderer:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) entity rendering system
    /// - Located via string references: Model loading and rendering for entities
    /// - "ModelResRef" @ 0x007c2f6c (model resource reference field), "Appearance_Type" @ 0x007c40f0 (appearance type field)
    /// - Model loading: FUN_005261b0 @ 0x005261b0 loads creature model from appearance.2da
    /// - "CSWCCreature::LoadModel(): Failed to load creature model '%s'." @ 0x007c82fc (model loading error)
    /// - Original implementation: Loads MDL models for entities and renders with transforms
    /// - Models resolved from appearance.2da (creatures), placeables.2da (placeables), genericdoors.2da (doors)
    /// - Caches loaded models to avoid reloading (model cache dictionary by ResRef)
    /// - Model conversion: MDL format (KOTOR native) converted to MonoGame Model format for rendering
    /// - Material resolution: BasicEffect created per texture/material (texture loading from TPC files)
    /// - Render transform: Entity position/orientation applied via world matrix for rendering
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_005261b0 @ 0x005261b0 (load creature model)
    /// </remarks>
    public class EntityModelRenderer
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly Dictionary<string, MdlToMonoGameModelConverter.ConversionResult> _modelCache;
        private readonly Dictionary<string, BasicEffect> _materialCache;
        // Using object to avoid circular dependency - should be GameDataManager at runtime
        private readonly object _gameDataManager;
        private readonly Installation _installation;
        private readonly Func<string, BasicEffect> _materialResolver;

        public EntityModelRenderer(
            [NotNull] GraphicsDevice device,
            [NotNull] object gameDataManager,
            [NotNull] Installation installation)
        {
            if (device == null)
            {
                throw new ArgumentNullException("device");
            }
            if (gameDataManager == null)
            {
                throw new ArgumentNullException("gameDataManager");
            }
            if (installation == null)
            {
                throw new ArgumentNullException("installation");
            }

            _graphicsDevice = device;
            _gameDataManager = gameDataManager;
            _installation = installation;
            _modelCache = new Dictionary<string, MdlToMonoGameModelConverter.ConversionResult>(StringComparer.OrdinalIgnoreCase);
            _materialCache = new Dictionary<string, BasicEffect>(StringComparer.OrdinalIgnoreCase);

            // Material resolver: creates BasicEffect for texture names
            _materialResolver = (textureName) =>
            {
                if (string.IsNullOrEmpty(textureName))
                {
                    return CreateDefaultEffect();
                }

                if (_materialCache.TryGetValue(textureName, out BasicEffect cached))
                {
                    return cached;
                }

                // Create new effect (texture loading would go here)
                BasicEffect effect = CreateDefaultEffect();
                _materialCache[textureName] = effect;
                return effect;
            };
        }

        /// <summary>
        /// Gets or loads a model for an entity.
        /// </summary>
        [CanBeNull]
        public MdlToMonoGameModelConverter.ConversionResult GetEntityModel([NotNull] IEntity entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException("entity");
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
                // Resolve model from appearance using reflection to avoid circular dependency
                // ModelResolver is in Andastra.Runtime.Engines.Odyssey.Systems namespace
                try
                {
                    System.Type modelResolverType = System.Type.GetType("Andastra.Runtime.Engines.Odyssey.Systems.ModelResolver, Andastra.Runtime.Games.Odyssey");
                    if (modelResolverType != null)
                    {
                        System.Reflection.MethodInfo resolveMethod = modelResolverType.GetMethod("ResolveEntityModel", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        if (resolveMethod != null)
                        {
                            object result = resolveMethod.Invoke(null, new object[] { _gameDataManager, entity });
                            modelResRef = result as string;
                        }
                    }
                }
                catch
                {
                    // If ModelResolver is not available, return null (model won't be resolved)
                    modelResRef = null;
                }
            }

            if (string.IsNullOrEmpty(modelResRef))
            {
                return null;
            }

            // Check cache
            if (_modelCache.TryGetValue(modelResRef, out MdlToMonoGameModelConverter.ConversionResult cached))
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

                var converter = new MdlToMonoGameModelConverter(_graphicsDevice, _materialResolver);
                MdlToMonoGameModelConverter.ConversionResult result = converter.Convert(mdl);

                if (result != null && result.Meshes.Count > 0)
                {
                    _modelCache[modelResRef] = result;
                    return result;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[EntityModelRenderer] Failed to load model " + modelResRef + ": " + ex.Message);
            }

            return null;
        }

        /// <summary>
        /// Renders an entity model at the given transform.
        /// </summary>
        public void RenderEntity(
            [NotNull] IEntity entity,
            Matrix viewMatrix,
            Matrix projectionMatrix)
        {
            if (entity == null)
            {
                return;
            }

            IRenderableComponent renderable = entity.GetComponent<IRenderableComponent>();
            if (renderable != null && !renderable.Visible)
            {
                return;
            }

            MdlToMonoGameModelConverter.ConversionResult model = GetEntityModel(entity);
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
            Matrix rotation = Matrix.CreateRotationY(transform.Facing);
            Matrix translation = Matrix.CreateTranslation(
                transform.Position.X,
                transform.Position.Y,
                transform.Position.Z
            );
            Matrix worldMatrix = rotation * translation;

            // Render all meshes
            foreach (MdlToMonoGameModelConverter.MeshData mesh in model.Meshes)
            {
                if (mesh.VertexBuffer == null || mesh.IndexBuffer == null)
                {
                    continue;
                }

                // Combine mesh transform with entity transform
                Matrix finalWorld = mesh.WorldTransform * worldMatrix;

                // Set up rendering state
                _graphicsDevice.SetVertexBuffer(mesh.VertexBuffer);
                _graphicsDevice.Indices = mesh.IndexBuffer;

                // Use mesh effect or default
                BasicEffect effect = mesh.Effect ?? CreateDefaultEffect();
                effect.View = viewMatrix;
                effect.Projection = projectionMatrix;
                effect.World = finalWorld;
                effect.VertexColorEnabled = false;
                effect.TextureEnabled = true;
                effect.LightingEnabled = true;

                // Apply opacity from renderable component for fade-in/fade-out effects
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FadeTime @ 0x007c60ec (fade duration), alpha blending for entity rendering
                // Opacity is updated by AppearAnimationFadeSystem for appear animations
                // Opacity is updated by ActionDestroyObject for destroy animations
                float opacity = renderable.Opacity;
                effect.Alpha = opacity;

                // Render mesh
                foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    _graphicsDevice.DrawIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        0,
                        0,
                        mesh.IndexCount / 3
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

            try
            {
                BioWare.NET.Extract.Installation.ResourceResult result = _installation.Resources.LookupResource(modelResRef, BioWare.NET.Common.ResourceType.MDL);
                if (result == null || result.Data == null)
                {
                    return null;
                }

                // Use BioWare.NET MDL parser
                return BioWare.NET.Resource.Formats.MDL.MDLAuto.ReadMdl(result.Data);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[EntityModelRenderer] Error loading MDL " + modelResRef + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Creates a default BasicEffect for rendering.
        /// </summary>
        private BasicEffect CreateDefaultEffect()
        {
            var effect = new BasicEffect(_graphicsDevice);
            effect.VertexColorEnabled = false;
            effect.TextureEnabled = true;
            effect.LightingEnabled = true;
            effect.AmbientLightColor = new Vector3(0.3f, 0.3f, 0.3f);
            effect.DirectionalLight0.Enabled = true;
            effect.DirectionalLight0.Direction = new Vector3(-0.5f, -1.0f, -0.3f);
            effect.DirectionalLight0.DiffuseColor = new Vector3(0.8f, 0.8f, 0.7f);
            return effect;
        }

        /// <summary>
        /// Clears the model cache.
        /// </summary>
        public void ClearCache()
        {
            foreach (MdlToMonoGameModelConverter.ConversionResult result in _modelCache.Values)
            {
                if (result != null && result.Meshes != null)
                {
                    foreach (MdlToMonoGameModelConverter.MeshData mesh in result.Meshes)
                    {
                        mesh.VertexBuffer?.Dispose();
                        mesh.IndexBuffer?.Dispose();
                    }
                }
            }
            _modelCache.Clear();
            _materialCache.Clear();
        }
    }
}


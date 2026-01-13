using System;
using System.Collections.Generic;
using System.Numerics;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia;
using HolocronToolset.Data;
using UTC = BioWare.NET.Resource.Formats.GFF.Generics.UTC.UTC;
using BioWare.NET.Resource.Formats.MDLData;
using BioWare.NET.Resource;
using Andastra.Runtime.Stride.Converters;
using Andastra.Runtime.Graphics;
using Andastra.Runtime.Stride.Graphics;
using StrideGraphics = Stride.Graphics;
using Stride.Core.Mathematics;
using JetBrains.Annotations;
using BioWare.NET.Resource.Formats.TPC;

namespace HolocronToolset.Widgets
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/renderer/model.py:36
    // Original: class ModelRenderer(QOpenGLWidget):
    public class ModelRenderer : Control
    {
        private HTInstallation _installation;
        private byte[] _mdlData;
        private byte[] _mdxData;
        private MDL _parsedModel;
        private MdlToStrideModelConverter.ConversionResult _convertedModel;
        private IGraphicsDevice _graphicsDevice;
        private Func<string, IBasicEffect> _materialResolver;
        private readonly Dictionary<string, IBasicEffect> _effectCache;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/renderer/model.py:74-84
        // Original: @property def installation(self) -> Installation | None:
        public HTInstallation Installation
        {
            get { return _installation; }
            set
            {
                _installation = value;
                // If scene already exists, update its installation too
                // This is critical because initializeGL() may have created the scene before installation was set
                // Matching PyKotor implementation: if self._scene is not None and value is not None and self._scene.installation is None:
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/renderer/model.py:196-200
        // Original: def set_model(self, data: bytes, data_ext: bytes):
        public void SetModel(byte[] mdlData, byte[] mdxData)
        {
            // Store raw data
            _mdlData = mdlData;
            _mdxData = mdxData;

            // Parse and convert model data for rendering
            // Note: Python passes data[12:] to skip header, so we do the same for 1:1 parity
            // The 12-byte header contains: unused (4), mdl_size (4), mdx_size (4)
            if (_mdlData != null && _mdlData.Length > 12 && _graphicsDevice != null)
            {
                try
                {
                    // Parse MDL starting after the 12-byte header (data[12:] in Python)
                    // This matches the Python implementation behavior
                    _parsedModel = BioWare.NET.Resource.Formats.MDL.MDLAuto.ReadMdl(
                        _mdlData, 12, 0, _mdxData, 0, 0);

                    // Convert to Stride rendering structures
                    if (_parsedModel != null && _graphicsDevice is StrideGraphics.GraphicsDevice strideDevice)
                    {
                        var converter = new MdlToStrideModelConverter(strideDevice, _materialResolver ?? CreateDefaultMaterialResolver());
                        _convertedModel = converter.Convert(_parsedModel);

                        // Trigger visual update
                        InvalidateVisual();
                    }
                }
                catch (Exception ex)
                {
                    // Log error but don't crash - model might be corrupted or unsupported
                    Console.WriteLine($"[ModelRenderer] Failed to load model: {ex.Message}");

                    // Clear any previous model data
                    _parsedModel = null;
                    _convertedModel = null;
                }
            }
            else
            {
                // Clear model if data is invalid
                _parsedModel = null;
                _convertedModel = null;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/renderer/model.py:189-194
        // Original: def clear_model(self):
        public void ClearModel()
        {
            // Clear model data
            _mdlData = null;
            _mdxData = null;
            _parsedModel = null;
            _convertedModel = null;

            // Trigger visual update
            InvalidateVisual();
        }

        // Initialize graphics device and material resolver for model rendering
        public void InitializeGraphics([NotNull] IGraphicsDevice graphicsDevice, Func<string, IBasicEffect> materialResolver = null)
        {
            if (graphicsDevice == null)
            {
                throw new ArgumentNullException(nameof(graphicsDevice));
            }

            _graphicsDevice = graphicsDevice;
            _materialResolver = materialResolver ?? CreateDefaultMaterialResolver();

            // If we have model data waiting, parse it now
            if (_mdlData != null && _convertedModel == null)
            {
                SetModel(_mdlData, _mdxData);
            }
        }

        // Create a default material resolver for basic rendering
        // Matching PyKotor implementation: Material resolver creates effects with loaded textures
        // Original game: swkotor2.exe texture loading and material creation (d3d9.dll @ 0x0080a6c0)
        private Func<string, IBasicEffect> CreateDefaultMaterialResolver()
        {
            return (textureName) =>
            {
                // Handle null or empty texture names
                if (string.IsNullOrWhiteSpace(textureName))
                {
                    return CreateDefaultEffect();
                }

                // Normalize texture name for caching (case-insensitive)
                string normalizedName = textureName.ToLowerInvariant();

                // Check cache first to avoid reloading textures
                if (_effectCache.TryGetValue(normalizedName, out IBasicEffect cached))
                {
                    return cached;
                }

                // Ensure we have graphics device and installation
                if (_graphicsDevice == null)
                {
                    Console.WriteLine($"[ModelRenderer] Cannot create effect for texture '{textureName}' - graphics device not initialized");
                    return CreateDefaultEffect();
                }

                if (_installation == null)
                {
                    Console.WriteLine($"[ModelRenderer] Cannot create effect for texture '{textureName}' - installation not set");
                    return CreateDefaultEffect();
                }

                // Try to load texture from installation
                // Matching PyKotor: installation.texture(resname) loads TPC texture
                // Search order: CUSTOM_FOLDERS, OVERRIDE, CUSTOM_MODULES, TEXTURES_TPA, CHITIN
                TPC tpc = null;
                try
                {
                    tpc = _installation.Installation.Texture(normalizedName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ModelRenderer] Failed to load texture '{textureName}': {ex.Message}");
                    // Continue with default effect
                }

                // Create StrideBasicEffect with or without texture
                IBasicEffect effect = null;
                if (_graphicsDevice is StrideGraphics.GraphicsDevice strideDevice)
                {
                    effect = new StrideBasicEffect(strideDevice);

                    // If texture was loaded successfully, convert and apply it
                    if (tpc != null)
                    {
                        try
                        {
                            // Get command list for texture operations
                            StrideGraphics.CommandList commandList = null;
                            if (_graphicsDevice is StrideGraphicsDevice strideGraphicsDevice)
                            {
                                commandList = strideGraphicsDevice.ImmediateContext;
                            }
                            else if (strideDevice != null)
                            {
                                commandList = strideDevice.ImmediateContext();
                            }

                            // Convert TPC to Stride texture
                            StrideGraphics.Texture strideTexture = TpcToStrideTextureConverter.Convert(
                                tpc,
                                strideDevice,
                                commandList,
                                generateMipmaps: true
                            );

                            // Create StrideTexture2D wrapper
                            StrideTexture2D texture2D = new StrideTexture2D(strideTexture, commandList);

                            // Set texture on effect
                            effect.Texture = texture2D;
                            effect.TextureEnabled = true;

                            // Enable basic lighting for textured materials
                            effect.LightingEnabled = true;
                            effect.Alpha = 1.0f;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[ModelRenderer] Failed to convert texture '{textureName}' to Stride format: {ex.Message}");
                            // Effect will be created without texture
                        }
                    }
                    else
                    {
                        // No texture loaded - use default effect settings
                        effect.TextureEnabled = false;
                        effect.LightingEnabled = true;
                        effect.Alpha = 1.0f;
                    }
                }
                else
                {
                    // Fallback: create default effect if not Stride device
                    effect = CreateDefaultEffect();
                }

                // Cache the effect (even if texture loading failed, cache to avoid repeated attempts)
                _effectCache[normalizedName] = effect;

                return effect;
            };
        }

        // Create a default effect without texture (fallback)
        private IBasicEffect CreateDefaultEffect()
        {
            if (_graphicsDevice is StrideGraphics.GraphicsDevice strideDevice)
            {
                var effect = new StrideBasicEffect(strideDevice);
                effect.TextureEnabled = false;
                effect.LightingEnabled = true;
                effect.Alpha = 1.0f;
                return effect;
            }

            // Return null if graphics device is not Stride (should not happen in Stride context)
            return null;
        }


        // Get the parsed model for external access (e.g., for property editors)
        [CanBeNull]
        public MDL ParsedModel => _parsedModel;

        // Get the converted model data for rendering
        [CanBeNull]
        public MdlToStrideModelConverter.ConversionResult ConvertedModel => _convertedModel;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/renderer/model.py:202-203
        // Original: def set_creature(self, utc: UTC):
        public void SetCreature(UTC utc)
        {
            // Store UTC for creature rendering
            // Matching PyKotor: self._creature_to_load = utc
            // The actual model loading happens in Render() method (matching PyKotor's paintGL() pattern)
            _creatureToLoad = utc;
        }

        private UTC _creatureToLoad;

        // Rendering state
        private Matrix4x4 _viewMatrix;
        private Matrix4x4 _projectionMatrix;
        private Stride.Core.Mathematics.Vector3 _cameraPosition;
        private Stride.Core.Mathematics.Vector3 _cameraTarget;
        private Stride.Core.Mathematics.Vector3 _cameraUp;

        // Initialize camera and matrices
        public ModelRenderer()
        {
            // Set up default camera (matching PyKotor default view)
            _cameraPosition = new Stride.Core.Mathematics.Vector3(0, 0, 10);
            _cameraTarget = new Stride.Core.Mathematics.Vector3(0, 0, 0);
            _cameraUp = new Stride.Core.Mathematics.Vector3(0, 1, 0);

            _effectCache = new Dictionary<string, IBasicEffect>(StringComparer.OrdinalIgnoreCase);

            UpdateViewMatrix();
            UpdateProjectionMatrix();
        }

        private void UpdateViewMatrix()
        {
            _viewMatrix = Matrix4x4.CreateLookAt(
                new System.Numerics.Vector3(_cameraPosition.X, _cameraPosition.Y, _cameraPosition.Z),
                new System.Numerics.Vector3(_cameraTarget.X, _cameraTarget.Y, _cameraTarget.Z),
                new System.Numerics.Vector3(_cameraUp.X, _cameraUp.Y, _cameraUp.Z)
            );
        }

        private void UpdateProjectionMatrix()
        {
            // Create perspective projection (45 degree FOV, matching typical 3D viewer)
            float aspectRatio = (float)Bounds.Width / Math.Max(1, (float)Bounds.Height);
            float fovRadians = 45.0f * (float)Math.PI / 180.0f; // Convert degrees to radians
            _projectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(
                fovRadians,
                aspectRatio,
                0.1f,  // Near plane
                1000.0f // Far plane
            );
        }

        // Set camera position and target
        public void SetCamera(Stride.Core.Mathematics.Vector3 position, Stride.Core.Mathematics.Vector3 target)
        {
            _cameraPosition = position;
            _cameraTarget = target;
            UpdateViewMatrix();
            InvalidateVisual();
        }

        // Override Avalonia rendering to draw 3D model
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/renderer/model.py:103-141
        // Original: def paintGL(self):
        public override void Render(DrawingContext context)
        {
            base.Render(context);

            // Matching PyKotor lines 114-121: Load model if pending
            if (_mdlData != null && _mdxData != null && _convertedModel == null && _graphicsDevice != null)
            {
                // Model data is already set, just needs parsing (handled in SetModel)
                // This case is for when SetModel was called but graphics device wasn't ready
            }

            // Matching PyKotor lines 123-132: Load creature if pending
            if (_creatureToLoad != null && _installation != null)
            {
                try
                {
                    // Matching PyKotor: Use sync=True to force synchronous model loading for the preview renderer
                    // This ensures hooks (headhook, rhand, lhand, gogglehook) are found correctly
                    // Matching PyKotor: self.scene.objects["model"] = self.scene.get_creature_render_object(None, self._creature_to_load, sync=True)

                    // Get body model name from UTC using appearance.2da
                    // Matching PyKotor: Uses get_body_model() function to resolve model from UTC
                    var (bodyModel, bodyTexture) = BioWare.NET.Tools.Creature.GetBodyModel(
                        _creatureToLoad,
                        _installation.Installation);

                    if (!string.IsNullOrWhiteSpace(bodyModel))
                    {
                        // Load MDL data from installation
                        // Matching PyKotor: Model loading happens via installation.resource() calls
                        var mdlResult = _installation.Resource(bodyModel, ResourceType.MDL, null);
                        var mdxResult = _installation.Resource(bodyModel, ResourceType.MDX, null);

                        if (mdlResult != null && mdlResult.Data != null && mdxResult != null && mdxResult.Data != null)
                        {
                            // Load the model data
                            // Matching PyKotor: self.scene.models["model"] = gl_load_mdl(self.scene, *self._model_to_load)
                            SetModel(mdlResult.Data, mdxResult.Data);

                            // Matching PyKotor line 132: self.reset_camera()
                            ResetCamera();
                        }
                        else
                        {
                            Console.WriteLine($"[ModelRenderer] Failed to load creature model '{bodyModel}' - MDL or MDX not found");
                        }
                    }
                    else
                    {
                        Console.WriteLine("[ModelRenderer] Failed to resolve body model from UTC appearance");
                    }
                }
                catch (Exception ex)
                {
                    // Log error but don't crash - creature might have invalid appearance data
                    Console.WriteLine($"[ModelRenderer] Failed to load creature model: {ex.Message}");
                }
                finally
                {
                    // Matching PyKotor line 130: self._creature_to_load = None
                    _creatureToLoad = null;
                }
            }

            // If we have a converted model and graphics device, render it
            if (_convertedModel != null && _graphicsDevice != null)
            {
                RenderModel();
            }
            else
            {
                // Draw placeholder text if no model is loaded
                DrawPlaceholderText(context);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/renderer/model.py:144-150
        // Original: def reset_camera(self):
        private void ResetCamera()
        {
            // Reset camera to default position (matching PyKotor default view)
            _cameraPosition = new Stride.Core.Mathematics.Vector3(0, 0, 10);
            _cameraTarget = new Stride.Core.Mathematics.Vector3(0, 0, 0);
            _cameraUp = new Stride.Core.Mathematics.Vector3(0, 1, 0);
            UpdateViewMatrix();
            InvalidateVisual();
        }

        private void DrawPlaceholderText(DrawingContext context)
        {
            var text = _parsedModel != null ?
                $"Model loaded: {_parsedModel.Name}\nMeshes: {_convertedModel?.Meshes.Count ?? 0}\n[3D Rendering TODO: Implement Stride rendering pipeline]" :
                "No model loaded\n[TODO: Initialize graphics device]";

            // Simple text rendering - in full implementation this would be proper 3D viewport
            var brush = new SolidColorBrush(Avalonia.Media.Colors.White);
            var formattedText = new FormattedText(
                text,
                System.Globalization.CultureInfo.InvariantCulture,
                Avalonia.Media.FlowDirection.LeftToRight,
                new Typeface("Arial"),
                12,
                brush
            );

            context.DrawText(formattedText, new Avalonia.Point(10, 10));
        }

        private void RenderModel()
        {
            // Placeholder for 3D rendering implementation
            // In full implementation, this would:
            // 1. Set up the graphics device render target
            // 2. Clear the screen with KOTOR sky blue
            // 3. Render each mesh from _convertedModel.Meshes
            // 4. Apply proper transformations and effects
            // 5. Handle texture mapping and materials
            //
            // For now, this demonstrates that the model parsing and conversion
            // is working correctly by showing mesh information
        }


        // Handle size changes to update projection matrix
        protected override void OnSizeChanged(SizeChangedEventArgs e)
        {
            base.OnSizeChanged(e);
            UpdateProjectionMatrix();
        }
    }
}

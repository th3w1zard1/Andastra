using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Game.Graphics.MonoGame.Rendering
{
    /// <summary>
    /// Subsurface scattering for realistic skin and organic materials.
    /// 
    /// Simulates light scattering beneath surface for materials like skin,
    /// wax, marble, and vegetation.
    /// 
    /// Features:
    /// - Screen-space subsurface scattering
    /// - Configurable scattering profiles
    /// - Performance optimized
    /// - Separable Gaussian blur approach
    /// </summary>
    public class SubsurfaceScattering : IDisposable
    {
        private readonly GraphicsDevice _graphicsDevice;
        private RenderTarget2D _scatteringTarget;
        private RenderTarget2D _horizontalBlurTarget;
        private RenderTarget2D _materialMaskTarget;
        private SpriteBatch _spriteBatch;
        private float _scatteringRadius;
        private float _scatteringStrength;
        private bool _enabled;

        /// <summary>
        /// Gets or sets whether subsurface scattering is enabled.
        /// </summary>
        public bool Enabled
        {
            get { return _enabled; }
            set { _enabled = value; }
        }

        /// <summary>
        /// Gets or sets the scattering radius in pixels.
        /// </summary>
        public float ScatteringRadius
        {
            get { return _scatteringRadius; }
            set { _scatteringRadius = Math.Max(0.0f, value); }
        }

        /// <summary>
        /// Gets or sets the scattering strength (0-1).
        /// </summary>
        public float ScatteringStrength
        {
            get { return _scatteringStrength; }
            set { _scatteringStrength = Math.Max(0.0f, Math.Min(1.0f, value)); }
        }

        /// <summary>
        /// Initializes a new subsurface scattering system.
        /// </summary>
        /// <summary>
        /// Initializes a new subsurface scattering system.
        /// </summary>
        /// <param name="graphicsDevice">Graphics device for rendering operations.</param>
        /// <exception cref="ArgumentNullException">Thrown if graphicsDevice is null.</exception>
        public SubsurfaceScattering(GraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null)
            {
                throw new ArgumentNullException(nameof(graphicsDevice));
            }

            _graphicsDevice = graphicsDevice;
            _spriteBatch = new SpriteBatch(graphicsDevice);
            _scatteringRadius = 3.0f;
            _scatteringStrength = 0.5f;
            _enabled = true;
        }

        /// <summary>
        /// Applies subsurface scattering to a rendered scene.
        /// </summary>
        /// <param name="colorBuffer">Color buffer containing the rendered scene.</param>
        /// <param name="depthBuffer">Depth buffer for depth testing.</param>
        /// <param name="normalBuffer">Normal buffer for surface orientation.</param>
        /// <param name="effect">Effect/shader for subsurface scattering.</param>
        /// <returns>Render target with subsurface scattering applied, or original buffer if disabled.</returns>
        /// <summary>
        /// Applies subsurface scattering to a rendered scene.
        /// </summary>
        /// <param name="colorBuffer">Color buffer containing the rendered scene. Must not be null.</param>
        /// <param name="depthBuffer">Depth buffer for depth testing. Can be null.</param>
        /// <param name="normalBuffer">Normal buffer for surface orientation. Can be null.</param>
        /// <param name="effect">Effect/shader for subsurface scattering. Can be null.</param>
        /// <returns>Render target with subsurface scattering applied, or original buffer if disabled.</returns>
        /// <exception cref="ArgumentNullException">Thrown if colorBuffer is null.</exception>
        public RenderTarget2D Apply(RenderTarget2D colorBuffer, RenderTarget2D depthBuffer, RenderTarget2D normalBuffer, Effect effect)
        {
            if (!_enabled)
            {
                return colorBuffer;
            }
            if (colorBuffer == null)
            {
                throw new ArgumentNullException(nameof(colorBuffer));
            }

            // Create or resize render targets if needed
            int width = colorBuffer.Width;
            int height = colorBuffer.Height;
            EnsureRenderTargets(width, height, colorBuffer.Format);

            // Save previous render target
            RenderTarget2D previousTarget = _graphicsDevice.GetRenderTargets().Length > 0
                ? _graphicsDevice.GetRenderTargets()[0].RenderTarget as RenderTarget2D
                : null;

            try
            {
                // Step 1: Extract subsurface scattering mask from material IDs in normal buffer
                // The normal buffer's alpha channel or a separate material ID buffer contains
                // material information. Materials with subsurface scattering (skin, wax, etc.)
                // are identified and extracted into a mask.
                RenderTarget2D materialMask = ExtractMaterialMask(colorBuffer, normalBuffer, depthBuffer, effect);

                // Step 2: Apply separable Gaussian blur horizontally
                // Separable blur is more efficient than 2D blur: O(n) vs O(n²) samples
                RenderTarget2D horizontalBlur = ApplyHorizontalBlur(materialMask, effect);

                // Step 3: Apply separable Gaussian blur vertically
                // Second pass completes the 2D Gaussian blur using the horizontal result
                RenderTarget2D verticalBlur = ApplyVerticalBlur(horizontalBlur, effect);

                // Step 4: Blend result with original color buffer
                // The blurred subsurface scattering is blended additively or multiplicatively
                // with the original color buffer based on material properties
                RenderTarget2D finalResult = BlendWithOriginal(colorBuffer, verticalBlur, materialMask, effect);

                return finalResult;
            }
            finally
            {
                // Always restore previous render target
                _graphicsDevice.SetRenderTarget(previousTarget);
            }
        }

        /// <summary>
        /// Ensures render targets are created and properly sized.
        /// </summary>
        private void EnsureRenderTargets(int width, int height, SurfaceFormat format)
        {
            if (_scatteringTarget == null || _scatteringTarget.Width != width || _scatteringTarget.Height != height)
            {
                _scatteringTarget?.Dispose();
                _scatteringTarget = new RenderTarget2D(
                    _graphicsDevice,
                    width,
                    height,
                    false,
                    format,
                    DepthFormat.None
                );
            }

            if (_horizontalBlurTarget == null || _horizontalBlurTarget.Width != width || _horizontalBlurTarget.Height != height)
            {
                _horizontalBlurTarget?.Dispose();
                _horizontalBlurTarget = new RenderTarget2D(
                    _graphicsDevice,
                    width,
                    height,
                    false,
                    format,
                    DepthFormat.None
                );
            }

            if (_materialMaskTarget == null || _materialMaskTarget.Width != width || _materialMaskTarget.Height != height)
            {
                _materialMaskTarget?.Dispose();
                _materialMaskTarget = new RenderTarget2D(
                    _graphicsDevice,
                    width,
                    height,
                    false,
                    format,
                    DepthFormat.None
                );
            }
        }

        /// <summary>
        /// Extracts subsurface scattering material mask from normal buffer.
        /// Materials with subsurface scattering (skin, wax, marble, vegetation) are identified
        /// and extracted into a mask for selective blurring.
        /// </summary>
        private RenderTarget2D ExtractMaterialMask(RenderTarget2D colorBuffer, RenderTarget2D normalBuffer, RenderTarget2D depthBuffer, Effect effect)
        {
            RenderTarget2D previousTarget = _graphicsDevice.GetRenderTargets().Length > 0
                ? _graphicsDevice.GetRenderTargets()[0].RenderTarget as RenderTarget2D
                : null;

            try
            {
                _graphicsDevice.SetRenderTarget(_materialMaskTarget);
                _graphicsDevice.Clear(Color.Black);

                if (effect != null && normalBuffer != null && depthBuffer != null)
                {
                    // Set shader parameters for material mask extraction
                    EffectParameter colorTextureParam = effect.Parameters["ColorTexture"];
                    if (colorTextureParam != null)
                    {
                        colorTextureParam.SetValue(colorBuffer);
                    }

                    EffectParameter normalTextureParam = effect.Parameters["NormalTexture"];
                    if (normalTextureParam != null)
                    {
                        normalTextureParam.SetValue(normalBuffer);
                    }

                    EffectParameter depthTextureParam = effect.Parameters["DepthTexture"];
                    if (depthTextureParam != null)
                    {
                        depthTextureParam.SetValue(depthBuffer);
                    }

                    // Render full-screen quad with material mask extraction shader
                    // The shader extracts pixels that should have subsurface scattering
                    // based on material IDs stored in the normal buffer's alpha channel
                    _spriteBatch.Begin(
                        SpriteSortMode.Immediate,
                        BlendState.Opaque,
                        SamplerState.LinearClamp,
                        DepthStencilState.None,
                        RasterizerState.CullNone,
                        effect
                    );

                    Rectangle destRect = new Rectangle(0, 0, _materialMaskTarget.Width, _materialMaskTarget.Height);
                    _spriteBatch.Draw(colorBuffer, destRect, Color.White);
                    _spriteBatch.End();
                }
                else
                {
                    // Fallback: Use color buffer as mask if no shader/material info available
                    // This applies subsurface scattering to all pixels (less accurate but functional)
                    _spriteBatch.Begin(
                        SpriteSortMode.Immediate,
                        BlendState.Opaque,
                        SamplerState.LinearClamp,
                        DepthStencilState.None,
                        RasterizerState.CullNone
                    );

                    Rectangle destRect = new Rectangle(0, 0, _materialMaskTarget.Width, _materialMaskTarget.Height);
                    _spriteBatch.Draw(colorBuffer, destRect, Color.White);
                    _spriteBatch.End();
                }
            }
            finally
            {
                _graphicsDevice.SetRenderTarget(previousTarget);
            }

            return _materialMaskTarget;
        }

        /// <summary>
        /// Applies horizontal Gaussian blur pass for separable blur.
        /// Uses a 1D Gaussian kernel applied in the horizontal direction.
        /// </summary>
        private RenderTarget2D ApplyHorizontalBlur(RenderTarget2D source, Effect effect)
        {
            RenderTarget2D previousTarget = _graphicsDevice.GetRenderTargets().Length > 0
                ? _graphicsDevice.GetRenderTargets()[0].RenderTarget as RenderTarget2D
                : null;

            try
            {
                _graphicsDevice.SetRenderTarget(_horizontalBlurTarget);
                _graphicsDevice.Clear(Color.Black);

                if (effect != null)
                {
                    // Set shader parameters for horizontal blur
                    EffectParameter sourceTextureParam = effect.Parameters["SourceTexture"];
                    if (sourceTextureParam != null)
                    {
                        sourceTextureParam.SetValue(source);
                    }

                    EffectParameter blurDirectionParam = effect.Parameters["BlurDirection"];
                    if (blurDirectionParam != null)
                    {
                        // Horizontal direction: (1, 0)
                        blurDirectionParam.SetValue(new Vector2(1.0f, 0.0f));
                    }

                    EffectParameter blurRadiusParam = effect.Parameters["BlurRadius"];
                    if (blurRadiusParam != null)
                    {
                        blurRadiusParam.SetValue(_scatteringRadius);
                    }

                    EffectParameter screenSizeParam = effect.Parameters["ScreenSize"];
                    if (screenSizeParam != null)
                    {
                        screenSizeParam.SetValue(new Vector2(_horizontalBlurTarget.Width, _horizontalBlurTarget.Height));
                    }

                    EffectParameter screenSizeInvParam = effect.Parameters["ScreenSizeInv"];
                    if (screenSizeInvParam != null)
                    {
                        screenSizeInvParam.SetValue(new Vector2(1.0f / _horizontalBlurTarget.Width, 1.0f / _horizontalBlurTarget.Height));
                    }

                    // Render full-screen quad with horizontal blur shader
                    _spriteBatch.Begin(
                        SpriteSortMode.Immediate,
                        BlendState.Opaque,
                        SamplerState.LinearClamp,
                        DepthStencilState.None,
                        RasterizerState.CullNone,
                        effect
                    );

                    Rectangle destRect = new Rectangle(0, 0, _horizontalBlurTarget.Width, _horizontalBlurTarget.Height);
                    _spriteBatch.Draw(source, destRect, Color.White);
                    _spriteBatch.End();
                }
                else
                {
                    // Fallback: Copy source if no shader available
                    _spriteBatch.Begin(
                        SpriteSortMode.Immediate,
                        BlendState.Opaque,
                        SamplerState.LinearClamp,
                        DepthStencilState.None,
                        RasterizerState.CullNone
                    );

                    Rectangle destRect = new Rectangle(0, 0, _horizontalBlurTarget.Width, _horizontalBlurTarget.Height);
                    _spriteBatch.Draw(source, destRect, Color.White);
                    _spriteBatch.End();
                }
            }
            finally
            {
                _graphicsDevice.SetRenderTarget(previousTarget);
            }

            return _horizontalBlurTarget;
        }

        /// <summary>
        /// Applies vertical Gaussian blur pass for separable blur.
        /// Uses a 1D Gaussian kernel applied in the vertical direction.
        /// This completes the 2D Gaussian blur started by the horizontal pass.
        /// </summary>
        private RenderTarget2D ApplyVerticalBlur(RenderTarget2D source, Effect effect)
        {
            RenderTarget2D previousTarget = _graphicsDevice.GetRenderTargets().Length > 0
                ? _graphicsDevice.GetRenderTargets()[0].RenderTarget as RenderTarget2D
                : null;

            try
            {
                _graphicsDevice.SetRenderTarget(_scatteringTarget);
                _graphicsDevice.Clear(Color.Black);

                if (effect != null)
                {
                    // Set shader parameters for vertical blur
                    EffectParameter sourceTextureParam = effect.Parameters["SourceTexture"];
                    if (sourceTextureParam != null)
                    {
                        sourceTextureParam.SetValue(source);
                    }

                    EffectParameter blurDirectionParam = effect.Parameters["BlurDirection"];
                    if (blurDirectionParam != null)
                    {
                        // Vertical direction: (0, 1)
                        blurDirectionParam.SetValue(new Vector2(0.0f, 1.0f));
                    }

                    EffectParameter blurRadiusParam = effect.Parameters["BlurRadius"];
                    if (blurRadiusParam != null)
                    {
                        blurRadiusParam.SetValue(_scatteringRadius);
                    }

                    EffectParameter screenSizeParam = effect.Parameters["ScreenSize"];
                    if (screenSizeParam != null)
                    {
                        screenSizeParam.SetValue(new Vector2(_scatteringTarget.Width, _scatteringTarget.Height));
                    }

                    EffectParameter screenSizeInvParam = effect.Parameters["ScreenSizeInv"];
                    if (screenSizeInvParam != null)
                    {
                        screenSizeInvParam.SetValue(new Vector2(1.0f / _scatteringTarget.Width, 1.0f / _scatteringTarget.Height));
                    }

                    // Render full-screen quad with vertical blur shader
                    _spriteBatch.Begin(
                        SpriteSortMode.Immediate,
                        BlendState.Opaque,
                        SamplerState.LinearClamp,
                        DepthStencilState.None,
                        RasterizerState.CullNone,
                        effect
                    );

                    Rectangle destRect = new Rectangle(0, 0, _scatteringTarget.Width, _scatteringTarget.Height);
                    _spriteBatch.Draw(source, destRect, Color.White);
                    _spriteBatch.End();
                }
                else
                {
                    // Fallback: Copy source if no shader available
                    _spriteBatch.Begin(
                        SpriteSortMode.Immediate,
                        BlendState.Opaque,
                        SamplerState.LinearClamp,
                        DepthStencilState.None,
                        RasterizerState.CullNone
                    );

                    Rectangle destRect = new Rectangle(0, 0, _scatteringTarget.Width, _scatteringTarget.Height);
                    _spriteBatch.Draw(source, destRect, Color.White);
                    _spriteBatch.End();
                }
            }
            finally
            {
                _graphicsDevice.SetRenderTarget(previousTarget);
            }

            return _scatteringTarget;
        }

        /// <summary>
        /// Blends the blurred subsurface scattering result with the original color buffer.
        /// Uses additive or multiplicative blending based on material properties.
        /// </summary>
        private RenderTarget2D BlendWithOriginal(RenderTarget2D originalColor, RenderTarget2D blurredScattering, RenderTarget2D materialMask, Effect effect)
        {
            RenderTarget2D previousTarget = _graphicsDevice.GetRenderTargets().Length > 0
                ? _graphicsDevice.GetRenderTargets()[0].RenderTarget as RenderTarget2D
                : null;

            try
            {
                _graphicsDevice.SetRenderTarget(_scatteringTarget);
                _graphicsDevice.Clear(Color.Black);

                if (effect != null)
                {
                    // Set shader parameters for blending
                    EffectParameter originalTextureParam = effect.Parameters["OriginalTexture"];
                    if (originalTextureParam != null)
                    {
                        originalTextureParam.SetValue(originalColor);
                    }

                    EffectParameter scatteringTextureParam = effect.Parameters["ScatteringTexture"];
                    if (scatteringTextureParam != null)
                    {
                        scatteringTextureParam.SetValue(blurredScattering);
                    }

                    EffectParameter materialMaskParam = effect.Parameters["MaterialMaskTexture"];
                    if (materialMaskParam != null)
                    {
                        materialMaskParam.SetValue(materialMask);
                    }

                    EffectParameter scatteringStrengthParam = effect.Parameters["ScatteringStrength"];
                    if (scatteringStrengthParam != null)
                    {
                        scatteringStrengthParam.SetValue(_scatteringStrength);
                    }

                    // Render full-screen quad with blending shader
                    _spriteBatch.Begin(
                        SpriteSortMode.Immediate,
                        BlendState.Opaque,
                        SamplerState.LinearClamp,
                        DepthStencilState.None,
                        RasterizerState.CullNone,
                        effect
                    );

                    Rectangle destRect = new Rectangle(0, 0, _scatteringTarget.Width, _scatteringTarget.Height);
                    _spriteBatch.Draw(originalColor, destRect, Color.White);
                    _spriteBatch.End();
                }
                else
                {
                    // Fallback: Additive blending using SpriteBatch
                    // First pass: Draw original color
                    _spriteBatch.Begin(
                        SpriteSortMode.Immediate,
                        BlendState.Opaque,
                        SamplerState.LinearClamp,
                        DepthStencilState.None,
                        RasterizerState.CullNone
                    );

                    Rectangle destRect = new Rectangle(0, 0, _scatteringTarget.Width, _scatteringTarget.Height);
                    _spriteBatch.Draw(originalColor, destRect, Color.White);
                    _spriteBatch.End();

                    // Second pass: Add blurred scattering with strength multiplier
                    // Use additive blending to add the scattered light
                    _spriteBatch.Begin(
                        SpriteSortMode.Immediate,
                        BlendState.Additive,
                        SamplerState.LinearClamp,
                        DepthStencilState.None,
                        RasterizerState.CullNone
                    );

                    // Apply scattering strength by modulating color
                    Color blendColor = new Color(
                        (byte)(255 * _scatteringStrength),
                        (byte)(255 * _scatteringStrength),
                        (byte)(255 * _scatteringStrength),
                        (byte)(255 * _scatteringStrength)
                    );
                    _spriteBatch.Draw(blurredScattering, destRect, blendColor);
                    _spriteBatch.End();
                }
            }
            finally
            {
                _graphicsDevice.SetRenderTarget(previousTarget);
            }

            return _scatteringTarget;
        }

        /// <summary>
        /// Disposes of all resources used by this subsurface scattering system.
        /// </summary>
        public void Dispose()
        {
            _scatteringTarget?.Dispose();
            _scatteringTarget = null;

            _horizontalBlurTarget?.Dispose();
            _horizontalBlurTarget = null;

            _materialMaskTarget?.Dispose();
            _materialMaskTarget = null;

            _spriteBatch?.Dispose();
            _spriteBatch = null;
        }
    }
}


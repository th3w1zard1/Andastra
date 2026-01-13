using System;
using Stride.Core.Mathematics;
using Stride.Graphics;

namespace Andastra.Game.Stride.Graphics
{
    /// <summary>
    /// Wrapper structs for Stride render state descriptions.
    /// These provide a compatible interface for the abstraction layer.
    /// </summary>

    /// <summary>
    /// Rasterizer state description for Stride.
    /// </summary>
    public struct RasterizerStateDescription
    {
        public CullMode CullMode;
        public FillMode FillMode;
        public float DepthBias;
        public float SlopeScaleDepthBias;
        public bool ScissorTestEnable;
        public bool MultiSampleAntiAlias;

        public static RasterizerStateDescription Default => new RasterizerStateDescription
        {
            CullMode = CullMode.Back,
            FillMode = FillMode.Solid,
            DepthBias = 0f,
            SlopeScaleDepthBias = 0f,
            ScissorTestEnable = false,
            MultiSampleAntiAlias = false
        };
    }

    /// <summary>
    /// Depth stencil state description for Stride.
    /// </summary>
    public struct DepthStencilStateDescription
    {
        public bool DepthBufferEnable;
        public bool DepthBufferWriteEnable;
        public CompareFunction DepthBufferFunction;
        public bool StencilEnable;
        public bool TwoSidedStencilMode;
        public StencilOperation StencilFail;
        public StencilOperation StencilDepthFail;
        public StencilOperation StencilPass;
        public CompareFunction StencilFunction;
        public int ReferenceStencil;
        public int StencilMask;
        public int StencilWriteMask;
        public int StencilReference; // Alias for ReferenceStencil
        public StencilFaceDescription FrontFace; // For two-sided stencil
        public StencilFaceDescription BackFace; // For two-sided stencil

        public static DepthStencilStateDescription Default => new DepthStencilStateDescription
        {
            DepthBufferEnable = true,
            DepthBufferWriteEnable = true,
            DepthBufferFunction = CompareFunction.LessEqual,
            StencilEnable = false,
            TwoSidedStencilMode = false,
            StencilFail = StencilOperation.Keep,
            StencilDepthFail = StencilOperation.Keep,
            StencilPass = StencilOperation.Keep,
            StencilFunction = CompareFunction.Always,
            ReferenceStencil = 0,
            StencilReference = 0,
            StencilMask = 255,
            StencilWriteMask = 255,
            FrontFace = new StencilFaceDescription
            {
                StencilFail = StencilOperation.Keep,
                StencilDepthFail = StencilOperation.Keep,
                StencilPass = StencilOperation.Keep,
                StencilFunction = CompareFunction.Always
            },
            BackFace = new StencilFaceDescription
            {
                StencilFail = StencilOperation.Keep,
                StencilDepthFail = StencilOperation.Keep,
                StencilPass = StencilOperation.Keep,
                StencilFunction = CompareFunction.Always
            }
        };
    }

    /// <summary>
    /// Stencil face description for two-sided stencil operations.
    /// </summary>
    public struct StencilFaceDescription
    {
        public StencilOperation StencilFail;
        public StencilOperation StencilDepthFail;
        public StencilOperation StencilPass;
        public CompareFunction StencilFunction;
    }

    /// <summary>
    /// Blend state description for Stride.
    /// This struct provides a compatible interface matching Stride's BlendStateDescription API,
    /// including support for multiple render targets and proper blend factor handling.
    /// </summary>
    public struct BlendStateDescription
    {
        /// <summary>
        /// Array of render target blend descriptions. Supports up to 8 render targets (D3D11 limit).
        /// Each render target can have independent blend settings.
        /// </summary>
        public RenderTargetBlendDescription[] RenderTargets;

        /// <summary>
        /// Global blend factor used for constant blending operations.
        /// This is a Color4 value where components are in the range [0.0, 1.0].
        /// </summary>
        public global::Stride.Core.Mathematics.Color4 BlendFactor;

        /// <summary>
        /// Multi-sample mask for alpha-to-coverage and multi-sample anti-aliasing.
        /// Value of -1 means all samples are enabled.
        /// </summary>
        public int MultiSampleMask;

        /// <summary>
        /// Alpha blend function for the first render target.
        /// </summary>
        public BlendFunction AlphaBlendFunction
        {
            get => RenderTargets != null && RenderTargets.Length > 0 ? RenderTargets[0].AlphaBlendFunction : BlendFunction.Add;
            set
            {
                EnsureRenderTargets();
                var rt = RenderTargets[0];
                rt.AlphaBlendFunction = value;
                RenderTargets[0] = rt;
            }
        }

        /// <summary>
        /// Alpha source blend factor for the first render target.
        /// </summary>
        public Blend AlphaSourceBlend
        {
            get => RenderTargets != null && RenderTargets.Length > 0 ? RenderTargets[0].AlphaSourceBlend : Blend.SourceAlpha;
            set
            {
                EnsureRenderTargets();
                var rt = RenderTargets[0];
                rt.AlphaSourceBlend = value;
                RenderTargets[0] = rt;
            }
        }

        /// <summary>
        /// Alpha destination blend factor for the first render target.
        /// </summary>
        public Blend AlphaDestinationBlend
        {
            get => RenderTargets != null && RenderTargets.Length > 0 ? RenderTargets[0].AlphaDestinationBlend : Blend.InverseSourceAlpha;
            set
            {
                EnsureRenderTargets();
                var rt = RenderTargets[0];
                rt.AlphaDestinationBlend = value;
                RenderTargets[0] = rt;
            }
        }

        /// <summary>
        /// Color blend function for the first render target.
        /// </summary>
        public BlendFunction ColorBlendFunction
        {
            get => RenderTargets != null && RenderTargets.Length > 0 ? RenderTargets[0].ColorBlendFunction : BlendFunction.Add;
            set
            {
                EnsureRenderTargets();
                var rt = RenderTargets[0];
                rt.ColorBlendFunction = value;
                RenderTargets[0] = rt;
            }
        }

        /// <summary>
        /// Color source blend factor for the first render target.
        /// </summary>
        public Blend ColorSourceBlend
        {
            get => RenderTargets != null && RenderTargets.Length > 0 ? RenderTargets[0].ColorSourceBlend : Blend.SourceAlpha;
            set
            {
                EnsureRenderTargets();
                var rt = RenderTargets[0];
                rt.ColorSourceBlend = value;
                RenderTargets[0] = rt;
            }
        }

        /// <summary>
        /// Color destination blend factor for the first render target.
        /// </summary>
        public Blend ColorDestinationBlend
        {
            get => RenderTargets != null && RenderTargets.Length > 0 ? RenderTargets[0].ColorDestinationBlend : Blend.InverseSourceAlpha;
            set
            {
                EnsureRenderTargets();
                var rt = RenderTargets[0];
                rt.ColorDestinationBlend = value;
                RenderTargets[0] = rt;
            }
        }

        /// <summary>
        /// Color write channels for the first render target.
        /// </summary>
        public ColorWriteChannels ColorWriteChannels
        {
            get => RenderTargets != null && RenderTargets.Length > 0 ? RenderTargets[0].ColorWriteChannels : ColorWriteChannels.All;
            set
            {
                EnsureRenderTargets();
                var rt = RenderTargets[0];
                rt.ColorWriteChannels = value;
                RenderTargets[0] = rt;
            }
        }

        /// <summary>
        /// Blend enable flag for the first render target.
        /// </summary>
        public bool BlendEnable
        {
            get => RenderTargets != null && RenderTargets.Length > 0 && RenderTargets[0].BlendEnable;
            set
            {
                EnsureRenderTargets();
                var rt = RenderTargets[0];
                rt.BlendEnable = value;
                RenderTargets[0] = rt;
            }
        }

        /// <summary>
        /// Ensures the RenderTargets array is initialized with at least one render target.
        /// Since this is a struct, we need to modify the field directly.
        /// </summary>
        private void EnsureRenderTargets()
        {
            if (RenderTargets == null || RenderTargets.Length == 0)
            {
                RenderTargets = new RenderTargetBlendDescription[1]
                {
                    new RenderTargetBlendDescription
                    {
                        BlendEnable = true,
                        AlphaBlendFunction = BlendFunction.Add,
                        AlphaSourceBlend = Blend.SourceAlpha,
                        AlphaDestinationBlend = Blend.InverseSourceAlpha,
                        ColorBlendFunction = BlendFunction.Add,
                        ColorSourceBlend = Blend.SourceAlpha,
                        ColorDestinationBlend = Blend.InverseSourceAlpha,
                        ColorWriteChannels = ColorWriteChannels.All
                    }
                };
            }
        }

        /// <summary>
        /// Creates a default blend state description with alpha blending enabled.
        /// </summary>
        public static BlendStateDescription Default()
        {
            return new BlendStateDescription
            {
                RenderTargets = new RenderTargetBlendDescription[1]
                {
                    new RenderTargetBlendDescription
                    {
                        BlendEnable = true,
                        AlphaBlendFunction = BlendFunction.Add,
                        AlphaSourceBlend = Blend.SourceAlpha,
                        AlphaDestinationBlend = Blend.InverseSourceAlpha,
                        ColorBlendFunction = BlendFunction.Add,
                        ColorSourceBlend = Blend.SourceAlpha,
                        ColorDestinationBlend = Blend.InverseSourceAlpha,
                        ColorWriteChannels = ColorWriteChannels.All
                    }
                },
                BlendFactor = new global::Stride.Core.Mathematics.Color4(0f, 0f, 0f, 1f),
                MultiSampleMask = -1
            };
        }

        /// <summary>
        /// Creates an alpha blend state description (same as Default).
        /// </summary>
        public static BlendStateDescription AlphaBlend()
        {
            return Default();
        }

        /// <summary>
        /// Creates an additive blend state description.
        /// Additive blending adds source and destination colors together.
        /// </summary>
        public static BlendStateDescription Additive()
        {
            var desc = Default();
            var rt = desc.RenderTargets[0];
            rt.ColorSourceBlend = Blend.One;
            rt.ColorDestinationBlend = Blend.One;
            rt.AlphaSourceBlend = Blend.One;
            rt.AlphaDestinationBlend = Blend.One;
            desc.RenderTargets[0] = rt;
            return desc;
        }

        /// <summary>
        /// Creates a blend state description with support for multiple render targets.
        /// </summary>
        /// <param name="renderTargetCount">Number of render targets to support (1-8).</param>
        /// <returns>A blend state description with the specified number of render targets.</returns>
        public static BlendStateDescription CreateMultiTarget(int renderTargetCount)
        {
            if (renderTargetCount < 1 || renderTargetCount > 8)
            {
                throw new ArgumentOutOfRangeException(nameof(renderTargetCount), "Render target count must be between 1 and 8.");
            }

            var renderTargets = new RenderTargetBlendDescription[renderTargetCount];
            var defaultRt = new RenderTargetBlendDescription
            {
                BlendEnable = true,
                AlphaBlendFunction = BlendFunction.Add,
                AlphaSourceBlend = Blend.SourceAlpha,
                AlphaDestinationBlend = Blend.InverseSourceAlpha,
                ColorBlendFunction = BlendFunction.Add,
                ColorSourceBlend = Blend.SourceAlpha,
                ColorDestinationBlend = Blend.InverseSourceAlpha,
                ColorWriteChannels = ColorWriteChannels.All
            };

            for (int i = 0; i < renderTargetCount; i++)
            {
                renderTargets[i] = defaultRt;
            }

            return new BlendStateDescription
            {
                RenderTargets = renderTargets,
                BlendFactor = new global::Stride.Core.Mathematics.Color4(0f, 0f, 0f, 1f),
                MultiSampleMask = -1
            };
        }
    }

    /// <summary>
    /// Blend description for a single render target.
    /// Each render target in a multi-render-target setup can have independent blend settings.
    /// </summary>
    public struct RenderTargetBlendDescription
    {
        /// <summary>
        /// Enable or disable blending for this render target.
        /// </summary>
        public bool BlendEnable;

        /// <summary>
        /// Alpha blend function (how alpha values are combined).
        /// </summary>
        public BlendFunction AlphaBlendFunction;

        /// <summary>
        /// Alpha source blend factor (what alpha value to use from the source).
        /// </summary>
        public Blend AlphaSourceBlend;

        /// <summary>
        /// Alpha destination blend factor (what alpha value to use from the destination).
        /// </summary>
        public Blend AlphaDestinationBlend;

        /// <summary>
        /// Color blend function (how color values are combined).
        /// </summary>
        public BlendFunction ColorBlendFunction;

        /// <summary>
        /// Color source blend factor (what color value to use from the source).
        /// </summary>
        public Blend ColorSourceBlend;

        /// <summary>
        /// Color destination blend factor (what color value to use from the destination).
        /// </summary>
        public Blend ColorDestinationBlend;

        /// <summary>
        /// Which color channels to write to (Red, Green, Blue, Alpha, or combinations).
        /// </summary>
        public ColorWriteChannels ColorWriteChannels;
    }

    /// <summary>
    /// Sampler state description for Stride.
    /// </summary>
    public struct SamplerStateDescription
    {
        public TextureAddressMode AddressU;
        public TextureAddressMode AddressV;
        public TextureAddressMode AddressW;
        public TextureFilter Filter;
        public int MaxAnisotropy;
        public int MaxMipLevel;
        public double MipMapLevelOfDetailBias;

        public static SamplerStateDescription Default => new SamplerStateDescription
        {
            AddressU = TextureAddressMode.Wrap,
            AddressV = TextureAddressMode.Wrap,
            AddressW = TextureAddressMode.Wrap,
            Filter = TextureFilter.Linear,
            MaxAnisotropy = 0,
            MaxMipLevel = 0,
            MipMapLevelOfDetailBias = 0.0
        };
    }

    /// <summary>
    /// Compare function enum for Stride compatibility.
    /// </summary>
    public enum CompareFunction
    {
        Never,
        Less,
        Equal,
        LessEqual,
        Greater,
        NotEqual,
        GreaterEqual,
        Always
    }

    /// <summary>
    /// Stencil operation enum for Stride compatibility.
    /// </summary>
    public enum StencilOperation
    {
        Keep,
        Zero,
        Replace,
        IncrementSaturation,
        DecrementSaturation,
        Invert,
        Increment,
        Decrement
    }

    /// <summary>
    /// Blend function enum for Stride compatibility.
    /// </summary>
    public enum BlendFunction
    {
        Add,
        Subtract,
        ReverseSubtract,
        Min,
        Max
    }

    /// <summary>
    /// Texture address mode enum for Stride compatibility.
    /// </summary>
    public enum TextureAddressMode
    {
        Wrap,
        Clamp,
        Mirror,
        Border
    }

    /// <summary>
    /// Texture filter enum for Stride compatibility.
    /// </summary>
    public enum TextureFilter
    {
        Linear,
        Point,
        Anisotropic,
        LinearMipPoint,
        PointMipLinear,
        MinLinearMagPointMipLinear,
        MinLinearMagPointMipPoint,
        MinPointMagLinearMipLinear,
        MinPointMagLinearMipPoint
    }
}


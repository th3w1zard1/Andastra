using System;
using Stride.Graphics;
using BioWareEngines.Graphics;

namespace BioWareEngines.Stride.Graphics
{
    /// <summary>
    /// Stride implementation of IRasterizerState.
    /// </summary>
    public class StrideRasterizerState : IRasterizerState
    {
        private RasterizerStateDescription _description;

        public StrideRasterizerState(RasterizerStateDescription? description = null)
        {
            _description = description ?? RasterizerStateDescription.Default;
        }

        public CullMode CullMode
        {
            get { return ConvertCullMode(_description.CullMode); }
            set
            {
                var desc = _description;
                desc.CullMode = ConvertCullMode(value);
                _description = desc;
            }
        }

        public FillMode FillMode
        {
            get { return ConvertFillMode(_description.FillMode); }
            set
            {
                var desc = _description;
                desc.FillMode = ConvertFillMode(value);
                _description = desc;
            }
        }

        public bool DepthBiasEnabled
        {
            get { return _description.DepthBias != 0; }
            set
            {
                var desc = _description;
                desc.DepthBias = value ? 0.00001f : 0f;
                _description = desc;
            }
        }

        public float DepthBias
        {
            get { return _description.DepthBias; }
            set
            {
                var desc = _description;
                desc.DepthBias = value;
                _description = desc;
            }
        }

        public float SlopeScaleDepthBias
        {
            get { return _description.SlopeScaleDepthBias; }
            set
            {
                var desc = _description;
                desc.SlopeScaleDepthBias = value;
                _description = desc;
            }
        }

        public bool ScissorTestEnabled
        {
            get { return _description.ScissorTestEnable; }
            set
            {
                var desc = _description;
                desc.ScissorTestEnable = value;
                _description = desc;
            }
        }

        public bool MultiSampleAntiAlias
        {
            get { return _description.MultiSampleAntiAlias; }
            set
            {
                var desc = _description;
                desc.MultiSampleAntiAlias = value;
                _description = desc;
            }
        }

        public void Dispose()
        {
            // RasterizerStateDescription is a struct, nothing to dispose
        }

        internal RasterizerStateDescription Description => _description;

        private static CullFaceMode ConvertCullMode(CullMode mode)
        {
            switch (mode)
            {
                case CullMode.None:
                    return CullFaceMode.None;
                case CullMode.CullClockwiseFace:
                    return CullFaceMode.Front;
                case CullMode.CullCounterClockwiseFace:
                    return CullFaceMode.Back;
                default:
                    return CullFaceMode.Back;
            }
        }

        private static CullMode ConvertCullMode(CullFaceMode mode)
        {
            switch (mode)
            {
                case CullFaceMode.None:
                    return CullMode.None;
                case CullFaceMode.Front:
                    return CullMode.CullClockwiseFace;
                case CullFaceMode.Back:
                    return CullMode.CullCounterClockwiseFace;
                default:
                    return CullMode.CullCounterClockwiseFace;
            }
        }

        private static Stride.Graphics.FillMode ConvertFillMode(FillMode mode)
        {
            switch (mode)
            {
                case FillMode.Solid:
                    return Stride.Graphics.FillMode.Solid;
                case FillMode.WireFrame:
                    return Stride.Graphics.FillMode.Wireframe;
                default:
                    return Stride.Graphics.FillMode.Solid;
            }
        }

        private static FillMode ConvertFillMode(Stride.Graphics.FillMode mode)
        {
            switch (mode)
            {
                case Stride.Graphics.FillMode.Solid:
                    return FillMode.Solid;
                case Stride.Graphics.FillMode.Wireframe:
                    return FillMode.WireFrame;
                default:
                    return FillMode.Solid;
            }
        }
    }

    /// <summary>
    /// Stride implementation of IDepthStencilState.
    /// </summary>
    public class StrideDepthStencilState : IDepthStencilState
    {
        private DepthStencilStateDescription _description;

        public StrideDepthStencilState(DepthStencilStateDescription? description = null)
        {
            _description = description ?? DepthStencilStateDescription.Default;
        }

        public bool DepthBufferEnable
        {
            get { return _description.DepthBufferEnable; }
            set
            {
                var desc = _description;
                desc.DepthBufferEnable = value;
                _description = desc;
            }
        }

        public bool DepthBufferWriteEnable
        {
            get { return _description.DepthBufferWriteEnable; }
            set
            {
                var desc = _description;
                desc.DepthBufferWriteEnable = value;
                _description = desc;
            }
        }

        public CompareFunction DepthBufferFunction
        {
            get { return ConvertCompareFunction(_description.DepthBufferFunction); }
            set
            {
                var desc = _description;
                desc.DepthBufferFunction = ConvertCompareFunction(value);
                _description = desc;
            }
        }

        public bool StencilEnable
        {
            get { return _description.StencilEnable; }
            set
            {
                var desc = _description;
                desc.StencilEnable = value;
                _description = desc;
            }
        }

        public bool TwoSidedStencilMode
        {
            get { return _description.TwoSidedStencilMode; }
            set
            {
                var desc = _description;
                desc.TwoSidedStencilMode = value;
                _description = desc;
            }
        }

        public StencilOperation StencilFail
        {
            get { return ConvertStencilOperation(_description.FrontFace.StencilFail); }
            set
            {
                var desc = _description;
                var frontFace = desc.FrontFace;
                frontFace.StencilFail = ConvertStencilOperation(value);
                desc.FrontFace = frontFace;
                _description = desc;
            }
        }

        public StencilOperation StencilDepthFail
        {
            get { return ConvertStencilOperation(_description.FrontFace.StencilDepthFail); }
            set
            {
                var desc = _description;
                var frontFace = desc.FrontFace;
                frontFace.StencilDepthFail = ConvertStencilOperation(value);
                desc.FrontFace = frontFace;
                _description = desc;
            }
        }

        public StencilOperation StencilPass
        {
            get { return ConvertStencilOperation(_description.FrontFace.StencilPass); }
            set
            {
                var desc = _description;
                var frontFace = desc.FrontFace;
                frontFace.StencilPass = ConvertStencilOperation(value);
                desc.FrontFace = frontFace;
                _description = desc;
            }
        }

        public CompareFunction StencilFunction
        {
            get { return ConvertCompareFunction(_description.FrontFace.StencilFunction); }
            set
            {
                var desc = _description;
                var frontFace = desc.FrontFace;
                frontFace.StencilFunction = ConvertCompareFunction(value);
                desc.FrontFace = frontFace;
                _description = desc;
            }
        }

        public int ReferenceStencil
        {
            get { return _description.StencilReference; }
            set
            {
                var desc = _description;
                desc.StencilReference = value;
                _description = desc;
            }
        }

        public int StencilMask
        {
            get { return (int)_description.StencilMask; }
            set
            {
                var desc = _description;
                desc.StencilMask = (uint)value;
                _description = desc;
            }
        }

        public int StencilWriteMask
        {
            get { return (int)_description.StencilWriteMask; }
            set
            {
                var desc = _description;
                desc.StencilWriteMask = (uint)value;
                _description = desc;
            }
        }

        public void Dispose()
        {
            // DepthStencilStateDescription is a struct, nothing to dispose
        }

        internal DepthStencilStateDescription Description => _description;

        private static Stride.Graphics.CompareFunction ConvertCompareFunction(CompareFunction func)
        {
            return (Stride.Graphics.CompareFunction)(int)func;
        }

        private static CompareFunction ConvertCompareFunction(Stride.Graphics.CompareFunction func)
        {
            return (CompareFunction)(int)func;
        }

        private static Stride.Graphics.StencilOperation ConvertStencilOperation(StencilOperation op)
        {
            return (Stride.Graphics.StencilOperation)(int)op;
        }

        private static StencilOperation ConvertStencilOperation(Stride.Graphics.StencilOperation op)
        {
            return (StencilOperation)(int)op;
        }
    }

    /// <summary>
    /// Stride implementation of IBlendState.
    /// </summary>
    public class StrideBlendState : IBlendState
    {
        private BlendStateDescription _description;

        public StrideBlendState(BlendStateDescription? description = null)
        {
            _description = description ?? BlendStateDescription.Default;
        }

        public BlendFunction AlphaBlendFunction
        {
            get { return ConvertBlendFunction(_description.AlphaSourceBlend); }
            set
            {
                var desc = _description;
                desc.AlphaSourceBlend = ConvertBlendFunction(value);
                _description = desc;
            }
        }

        public Blend AlphaDestinationBlend
        {
            get { return ConvertBlend(_description.AlphaDestinationBlend); }
            set
            {
                var desc = _description;
                desc.AlphaDestinationBlend = ConvertBlend(value);
                _description = desc;
            }
        }

        public Blend AlphaSourceBlend
        {
            get { return ConvertBlend(_description.AlphaSourceBlend); }
            set
            {
                var desc = _description;
                desc.AlphaSourceBlend = ConvertBlend(value);
                _description = desc;
            }
        }

        public BlendFunction ColorBlendFunction
        {
            get { return ConvertBlendFunction(_description.ColorSourceBlend); }
            set
            {
                var desc = _description;
                desc.ColorSourceBlend = ConvertBlendFunction(value);
                _description = desc;
            }
        }

        public Blend ColorDestinationBlend
        {
            get { return ConvertBlend(_description.ColorDestinationBlend); }
            set
            {
                var desc = _description;
                desc.ColorDestinationBlend = ConvertBlend(value);
                _description = desc;
            }
        }

        public Blend ColorSourceBlend
        {
            get { return ConvertBlend(_description.ColorSourceBlend); }
            set
            {
                var desc = _description;
                desc.ColorSourceBlend = ConvertBlend(value);
                _description = desc;
            }
        }

        public ColorWriteChannels ColorWriteChannels
        {
            get { return ConvertColorWriteChannels(_description.RenderTargets[0].ColorWriteChannels); }
            set
            {
                var desc = _description;
                var rt = desc.RenderTargets[0];
                rt.ColorWriteChannels = ConvertColorWriteChannels(value);
                desc.RenderTargets[0] = rt;
                _description = desc;
            }
        }

        public ColorWriteChannels ColorWriteChannels1
        {
            get { return ConvertColorWriteChannels(_description.RenderTargets.Length > 1 ? _description.RenderTargets[1].ColorWriteChannels : ColorWriteChannels.None); }
            set
            {
                if (_description.RenderTargets.Length > 1)
                {
                    var desc = _description;
                    var rt = desc.RenderTargets[1];
                    rt.ColorWriteChannels = ConvertColorWriteChannels(value);
                    desc.RenderTargets[1] = rt;
                    _description = desc;
                }
            }
        }

        public ColorWriteChannels ColorWriteChannels2
        {
            get { return ConvertColorWriteChannels(_description.RenderTargets.Length > 2 ? _description.RenderTargets[2].ColorWriteChannels : ColorWriteChannels.None); }
            set
            {
                if (_description.RenderTargets.Length > 2)
                {
                    var desc = _description;
                    var rt = desc.RenderTargets[2];
                    rt.ColorWriteChannels = ConvertColorWriteChannels(value);
                    desc.RenderTargets[2] = rt;
                    _description = desc;
                }
            }
        }

        public ColorWriteChannels ColorWriteChannels3
        {
            get { return ConvertColorWriteChannels(_description.RenderTargets.Length > 3 ? _description.RenderTargets[3].ColorWriteChannels : ColorWriteChannels.None); }
            set
            {
                if (_description.RenderTargets.Length > 3)
                {
                    var desc = _description;
                    var rt = desc.RenderTargets[3];
                    rt.ColorWriteChannels = ConvertColorWriteChannels(value);
                    desc.RenderTargets[3] = rt;
                    _description = desc;
                }
            }
        }

        public bool BlendEnable
        {
            get { return _description.RenderTargets[0].BlendEnable; }
            set
            {
                var desc = _description;
                var rt = desc.RenderTargets[0];
                rt.BlendEnable = value;
                desc.RenderTargets[0] = rt;
                _description = desc;
            }
        }

        public Color BlendFactor
        {
            get { return ConvertColor(_description.BlendFactor); }
            set
            {
                var desc = _description;
                desc.BlendFactor = ConvertColor(value);
                _description = desc;
            }
        }

        public int MultiSampleMask
        {
            get { return _description.MultiSampleMask; }
            set
            {
                var desc = _description;
                desc.MultiSampleMask = value;
                _description = desc;
            }
        }

        public void Dispose()
        {
            // BlendStateDescription is a struct, nothing to dispose
        }

        internal BlendStateDescription Description => _description;

        private static BlendFunction ConvertBlendFunction(Blend blend)
        {
            // This is a simplified conversion - Stride uses Blend enum for both source and function
            return BlendFunction.Add; // Default
        }

        private static Stride.Graphics.Blend ConvertBlendFunction(BlendFunction func)
        {
            // This is a simplified conversion
            return Stride.Graphics.Blend.One; // Default
        }

        private static Stride.Graphics.Blend ConvertBlend(Blend blend)
        {
            return (Stride.Graphics.Blend)(int)blend;
        }

        private static Blend ConvertBlend(Stride.Graphics.Blend blend)
        {
            return (Blend)(int)blend;
        }

        private static Stride.Graphics.ColorWriteChannels ConvertColorWriteChannels(ColorWriteChannels channels)
        {
            return (Stride.Graphics.ColorWriteChannels)(int)channels;
        }

        private static ColorWriteChannels ConvertColorWriteChannels(Stride.Graphics.ColorWriteChannels channels)
        {
            return (ColorWriteChannels)(int)channels;
        }

        private static Stride.Core.Mathematics.Color4 ConvertColor(Color color)
        {
            return new Stride.Core.Mathematics.Color4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, color.A / 255.0f);
        }

        private static Color ConvertColor(Stride.Core.Mathematics.Color4 color)
        {
            return new Color((byte)(color.R * 255), (byte)(color.G * 255), (byte)(color.B * 255), (byte)(color.A * 255));
        }
    }

    /// <summary>
    /// Stride implementation of ISamplerState.
    /// </summary>
    public class StrideSamplerState : ISamplerState
    {
        private SamplerStateDescription _description;

        public StrideSamplerState(SamplerStateDescription? description = null)
        {
            _description = description ?? SamplerStateDescription.Default;
        }

        public TextureAddressMode AddressU
        {
            get { return ConvertTextureAddressMode(_description.AddressU); }
            set
            {
                var desc = _description;
                desc.AddressU = ConvertTextureAddressMode(value);
                _description = desc;
            }
        }

        public TextureAddressMode AddressV
        {
            get { return ConvertTextureAddressMode(_description.AddressV); }
            set
            {
                var desc = _description;
                desc.AddressV = ConvertTextureAddressMode(value);
                _description = desc;
            }
        }

        public TextureAddressMode AddressW
        {
            get { return ConvertTextureAddressMode(_description.AddressW); }
            set
            {
                var desc = _description;
                desc.AddressW = ConvertTextureAddressMode(value);
                _description = desc;
            }
        }

        public TextureFilter Filter
        {
            get { return ConvertTextureFilter(_description.Filter); }
            set
            {
                var desc = _description;
                desc.Filter = ConvertTextureFilter(value);
                _description = desc;
            }
        }

        public int MaxAnisotropy
        {
            get { return _description.MaxAnisotropy; }
            set
            {
                var desc = _description;
                desc.MaxAnisotropy = value;
                _description = desc;
            }
        }

        public int MaxMipLevel
        {
            get { return _description.MaxMipLevels; }
            set
            {
                var desc = _description;
                desc.MaxMipLevels = value;
                _description = desc;
            }
        }

        public float MipMapLevelOfDetailBias
        {
            get { return _description.MipMapLevelOfDetailBias; }
            set
            {
                var desc = _description;
                desc.MipMapLevelOfDetailBias = value;
                _description = desc;
            }
        }

        public void Dispose()
        {
            // SamplerStateDescription is a struct, nothing to dispose
        }

        internal SamplerStateDescription Description => _description;

        private static Stride.Graphics.TextureAddressMode ConvertTextureAddressMode(TextureAddressMode mode)
        {
            return (Stride.Graphics.TextureAddressMode)(int)mode;
        }

        private static TextureAddressMode ConvertTextureAddressMode(Stride.Graphics.TextureAddressMode mode)
        {
            return (TextureAddressMode)(int)mode;
        }

        private static Stride.Graphics.TextureFilter ConvertTextureFilter(TextureFilter filter)
        {
            return (Stride.Graphics.TextureFilter)(int)filter;
        }

        private static TextureFilter ConvertTextureFilter(Stride.Graphics.TextureFilter filter)
        {
            return (TextureFilter)(int)filter;
        }
    }
}


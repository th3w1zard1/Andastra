using System;
using Microsoft.Xna.Framework.Graphics;
using Odyssey.Graphics;

namespace Odyssey.MonoGame.Graphics
{
    /// <summary>
    /// MonoGame implementation of IRasterizerState.
    /// </summary>
    public class MonoGameRasterizerState : IRasterizerState
    {
        private readonly RasterizerState _state;

        public MonoGameRasterizerState(RasterizerState state = null)
        {
            _state = state ?? new RasterizerState();
        }

        public CullMode CullMode
        {
            get { return ConvertCullMode(_state.CullMode); }
            set { _state.CullMode = ConvertCullMode(value); }
        }

        public FillMode FillMode
        {
            get { return ConvertFillMode(_state.FillMode); }
            set { _state.FillMode = ConvertFillMode(value); }
        }

        public bool DepthBiasEnabled
        {
            get { return _state.DepthBias != 0; }
            set { _state.DepthBias = value ? 0.00001f : 0f; }
        }

        public float DepthBias
        {
            get { return _state.DepthBias; }
            set { _state.DepthBias = value; }
        }

        public float SlopeScaleDepthBias
        {
            get { return _state.SlopeScaleDepthBias; }
            set { _state.SlopeScaleDepthBias = value; }
        }

        public bool ScissorTestEnabled
        {
            get { return _state.ScissorTestEnable; }
            set { _state.ScissorTestEnable = value; }
        }

        public bool MultiSampleAntiAlias
        {
            get { return _state.MultiSampleAntiAlias; }
            set { _state.MultiSampleAntiAlias = value; }
        }

        public void Dispose()
        {
            // RasterizerState is managed by MonoGame, don't dispose
        }

        internal RasterizerState State => _state;

        private static Microsoft.Xna.Framework.Graphics.CullMode ConvertCullMode(CullMode mode)
        {
            switch (mode)
            {
                case CullMode.None:
                    return Microsoft.Xna.Framework.Graphics.CullMode.None;
                case CullMode.CullClockwiseFace:
                    return Microsoft.Xna.Framework.Graphics.CullMode.CullClockwiseFace;
                case CullMode.CullCounterClockwiseFace:
                    return Microsoft.Xna.Framework.Graphics.CullMode.CullCounterClockwiseFace;
                default:
                    return Microsoft.Xna.Framework.Graphics.CullMode.CullCounterClockwiseFace;
            }
        }

        private static CullMode ConvertCullMode(Microsoft.Xna.Framework.Graphics.CullMode mode)
        {
            switch (mode)
            {
                case Microsoft.Xna.Framework.Graphics.CullMode.None:
                    return CullMode.None;
                case Microsoft.Xna.Framework.Graphics.CullMode.CullClockwiseFace:
                    return CullMode.CullClockwiseFace;
                case Microsoft.Xna.Framework.Graphics.CullMode.CullCounterClockwiseFace:
                    return CullMode.CullCounterClockwiseFace;
                default:
                    return CullMode.CullCounterClockwiseFace;
            }
        }

        private static Microsoft.Xna.Framework.Graphics.FillMode ConvertFillMode(FillMode mode)
        {
            switch (mode)
            {
                case FillMode.Solid:
                    return Microsoft.Xna.Framework.Graphics.FillMode.Solid;
                case FillMode.WireFrame:
                    return Microsoft.Xna.Framework.Graphics.FillMode.WireFrame;
                default:
                    return Microsoft.Xna.Framework.Graphics.FillMode.Solid;
            }
        }

        private static FillMode ConvertFillMode(Microsoft.Xna.Framework.Graphics.FillMode mode)
        {
            switch (mode)
            {
                case Microsoft.Xna.Framework.Graphics.FillMode.Solid:
                    return FillMode.Solid;
                case Microsoft.Xna.Framework.Graphics.FillMode.WireFrame:
                    return FillMode.WireFrame;
                default:
                    return FillMode.Solid;
            }
        }
    }

    /// <summary>
    /// MonoGame implementation of IDepthStencilState.
    /// </summary>
    public class MonoGameDepthStencilState : IDepthStencilState
    {
        private readonly DepthStencilState _state;

        public MonoGameDepthStencilState(DepthStencilState state = null)
        {
            _state = state ?? new DepthStencilState();
        }

        public bool DepthBufferEnable
        {
            get { return _state.DepthBufferEnable; }
            set { _state.DepthBufferEnable = value; }
        }

        public bool DepthBufferWriteEnable
        {
            get { return _state.DepthBufferWriteEnable; }
            set { _state.DepthBufferWriteEnable = value; }
        }

        public CompareFunction DepthBufferFunction
        {
            get { return ConvertCompareFunction(_state.DepthBufferFunction); }
            set { _state.DepthBufferFunction = ConvertCompareFunction(value); }
        }

        public bool StencilEnable
        {
            get { return _state.StencilEnable; }
            set { _state.StencilEnable = value; }
        }

        public bool TwoSidedStencilMode
        {
            get { return _state.TwoSidedStencilMode; }
            set { _state.TwoSidedStencilMode = value; }
        }

        public StencilOperation StencilFail
        {
            get { return ConvertStencilOperation(_state.StencilFail); }
            set { _state.StencilFail = ConvertStencilOperation(value); }
        }

        public StencilOperation StencilDepthFail
        {
            get { return ConvertStencilOperation(_state.StencilDepthFail); }
            set { _state.StencilDepthFail = ConvertStencilOperation(value); }
        }

        public StencilOperation StencilPass
        {
            get { return ConvertStencilOperation(_state.StencilPass); }
            set { _state.StencilPass = ConvertStencilOperation(value); }
        }

        public CompareFunction StencilFunction
        {
            get { return ConvertCompareFunction(_state.StencilFunction); }
            set { _state.StencilFunction = ConvertCompareFunction(value); }
        }

        public int ReferenceStencil
        {
            get { return _state.ReferenceStencil; }
            set { _state.ReferenceStencil = value; }
        }

        public int StencilMask
        {
            get { return _state.StencilMask; }
            set { _state.StencilMask = value; }
        }

        public int StencilWriteMask
        {
            get { return _state.StencilWriteMask; }
            set { _state.StencilWriteMask = value; }
        }

        public void Dispose()
        {
            // DepthStencilState is managed by MonoGame, don't dispose
        }

        internal DepthStencilState State => _state;

        private static Microsoft.Xna.Framework.Graphics.CompareFunction ConvertCompareFunction(CompareFunction func)
        {
            return (Microsoft.Xna.Framework.Graphics.CompareFunction)(int)func;
        }

        private static CompareFunction ConvertCompareFunction(Microsoft.Xna.Framework.Graphics.CompareFunction func)
        {
            return (CompareFunction)(int)func;
        }

        private static Microsoft.Xna.Framework.Graphics.StencilOperation ConvertStencilOperation(StencilOperation op)
        {
            return (Microsoft.Xna.Framework.Graphics.StencilOperation)(int)op;
        }

        private static StencilOperation ConvertStencilOperation(Microsoft.Xna.Framework.Graphics.StencilOperation op)
        {
            return (StencilOperation)(int)op;
        }
    }

    /// <summary>
    /// MonoGame implementation of IBlendState.
    /// </summary>
    public class MonoGameBlendState : IBlendState
    {
        private readonly BlendState _state;

        public MonoGameBlendState(BlendState state = null)
        {
            _state = state ?? new BlendState();
        }

        public BlendFunction AlphaBlendFunction
        {
            get { return ConvertBlendFunction(_state.AlphaBlendFunction); }
            set { _state.AlphaBlendFunction = ConvertBlendFunction(value); }
        }

        public Blend AlphaDestinationBlend
        {
            get { return ConvertBlend(_state.AlphaDestinationBlend); }
            set { _state.AlphaDestinationBlend = ConvertBlend(value); }
        }

        public Blend AlphaSourceBlend
        {
            get { return ConvertBlend(_state.AlphaSourceBlend); }
            set { _state.AlphaSourceBlend = ConvertBlend(value); }
        }

        public BlendFunction ColorBlendFunction
        {
            get { return ConvertBlendFunction(_state.ColorBlendFunction); }
            set { _state.ColorBlendFunction = ConvertBlendFunction(value); }
        }

        public Blend ColorDestinationBlend
        {
            get { return ConvertBlend(_state.ColorDestinationBlend); }
            set { _state.ColorDestinationBlend = ConvertBlend(value); }
        }

        public Blend ColorSourceBlend
        {
            get { return ConvertBlend(_state.ColorSourceBlend); }
            set { _state.ColorSourceBlend = ConvertBlend(value); }
        }

        public ColorWriteChannels ColorWriteChannels
        {
            get { return ConvertColorWriteChannels(_state.ColorWriteChannels); }
            set { _state.ColorWriteChannels = ConvertColorWriteChannels(value); }
        }

        public ColorWriteChannels ColorWriteChannels1
        {
            get { return ConvertColorWriteChannels(_state.ColorWriteChannels1); }
            set { _state.ColorWriteChannels1 = ConvertColorWriteChannels(value); }
        }

        public ColorWriteChannels ColorWriteChannels2
        {
            get { return ConvertColorWriteChannels(_state.ColorWriteChannels2); }
            set { _state.ColorWriteChannels2 = ConvertColorWriteChannels(value); }
        }

        public ColorWriteChannels ColorWriteChannels3
        {
            get { return ConvertColorWriteChannels(_state.ColorWriteChannels3); }
            set { _state.ColorWriteChannels3 = ConvertColorWriteChannels(value); }
        }

        public bool BlendEnable
        {
            get { return _state.ColorSourceBlend != Microsoft.Xna.Framework.Graphics.Blend.One || _state.AlphaSourceBlend != Microsoft.Xna.Framework.Graphics.Blend.One; }
            set
            {
                if (value)
                {
                    if (_state.ColorSourceBlend == Microsoft.Xna.Framework.Graphics.Blend.One)
                    {
                        _state.ColorSourceBlend = Microsoft.Xna.Framework.Graphics.Blend.SourceAlpha;
                    }
                    if (_state.AlphaSourceBlend == Microsoft.Xna.Framework.Graphics.Blend.One)
                    {
                        _state.AlphaSourceBlend = Microsoft.Xna.Framework.Graphics.Blend.SourceAlpha;
                    }
                }
                else
                {
                    _state.ColorSourceBlend = Microsoft.Xna.Framework.Graphics.Blend.One;
                    _state.AlphaSourceBlend = Microsoft.Xna.Framework.Graphics.Blend.One;
                }
            }
        }

        public Color BlendFactor
        {
            get { return ConvertColor(_state.BlendFactor); }
            set { _state.BlendFactor = ConvertColor(value); }
        }

        public int MultiSampleMask
        {
            get { return _state.MultiSampleMask; }
            set { _state.MultiSampleMask = value; }
        }

        public void Dispose()
        {
            // BlendState is managed by MonoGame, don't dispose
        }

        internal BlendState State => _state;

        private static Microsoft.Xna.Framework.Graphics.BlendFunction ConvertBlendFunction(BlendFunction func)
        {
            return (Microsoft.Xna.Framework.Graphics.BlendFunction)(int)func;
        }

        private static BlendFunction ConvertBlendFunction(Microsoft.Xna.Framework.Graphics.BlendFunction func)
        {
            return (BlendFunction)(int)func;
        }

        private static Microsoft.Xna.Framework.Graphics.Blend ConvertBlend(Blend blend)
        {
            return (Microsoft.Xna.Framework.Graphics.Blend)(int)blend;
        }

        private static Blend ConvertBlend(Microsoft.Xna.Framework.Graphics.Blend blend)
        {
            return (Blend)(int)blend;
        }

        private static Microsoft.Xna.Framework.Graphics.ColorWriteChannels ConvertColorWriteChannels(ColorWriteChannels channels)
        {
            return (Microsoft.Xna.Framework.Graphics.ColorWriteChannels)(int)channels;
        }

        private static ColorWriteChannels ConvertColorWriteChannels(Microsoft.Xna.Framework.Graphics.ColorWriteChannels channels)
        {
            return (ColorWriteChannels)(int)channels;
        }

        private static Microsoft.Xna.Framework.Color ConvertColor(Color color)
        {
            return new Microsoft.Xna.Framework.Color(color.R, color.G, color.B, color.A);
        }

        private static Color ConvertColor(Microsoft.Xna.Framework.Color color)
        {
            return new Color(color.R, color.G, color.B, color.A);
        }
    }

    /// <summary>
    /// MonoGame implementation of ISamplerState.
    /// </summary>
    public class MonoGameSamplerState : ISamplerState
    {
        private readonly SamplerState _state;

        public MonoGameSamplerState(SamplerState state = null)
        {
            _state = state ?? new SamplerState();
        }

        public TextureAddressMode AddressU
        {
            get { return ConvertTextureAddressMode(_state.AddressU); }
            set { _state.AddressU = ConvertTextureAddressMode(value); }
        }

        public TextureAddressMode AddressV
        {
            get { return ConvertTextureAddressMode(_state.AddressV); }
            set { _state.AddressV = ConvertTextureAddressMode(value); }
        }

        public TextureAddressMode AddressW
        {
            get { return ConvertTextureAddressMode(_state.AddressW); }
            set { _state.AddressW = ConvertTextureAddressMode(value); }
        }

        public TextureFilter Filter
        {
            get { return ConvertTextureFilter(_state.Filter); }
            set { _state.Filter = ConvertTextureFilter(value); }
        }

        public int MaxAnisotropy
        {
            get { return _state.MaxAnisotropy; }
            set { _state.MaxAnisotropy = value; }
        }

        public int MaxMipLevel
        {
            get { return _state.MaxMipLevel; }
            set { _state.MaxMipLevel = value; }
        }

        public float MipMapLevelOfDetailBias
        {
            get { return _state.MipMapLevelOfDetailBias; }
            set { _state.MipMapLevelOfDetailBias = value; }
        }

        public void Dispose()
        {
            // SamplerState is managed by MonoGame, don't dispose
        }

        internal SamplerState State => _state;

        private static Microsoft.Xna.Framework.Graphics.TextureAddressMode ConvertTextureAddressMode(TextureAddressMode mode)
        {
            return (Microsoft.Xna.Framework.Graphics.TextureAddressMode)(int)mode;
        }

        private static TextureAddressMode ConvertTextureAddressMode(Microsoft.Xna.Framework.Graphics.TextureAddressMode mode)
        {
            return (TextureAddressMode)(int)mode;
        }

        private static Microsoft.Xna.Framework.Graphics.TextureFilter ConvertTextureFilter(TextureFilter filter)
        {
            return (Microsoft.Xna.Framework.Graphics.TextureFilter)(int)filter;
        }

        private static TextureFilter ConvertTextureFilter(Microsoft.Xna.Framework.Graphics.TextureFilter filter)
        {
            return (TextureFilter)(int)filter;
        }
    }
}


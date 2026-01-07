using System;
using Andastra.Runtime.Graphics;
using Stride.Core.Mathematics;
using StrideGraphics = Stride.Graphics;

namespace Andastra.Runtime.Stride.Graphics
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

        public Andastra.Runtime.Graphics.CullMode CullMode
        {
            get { return ConvertCullMode(_description.CullMode); }
            set
            {
                var desc = _description;
                desc.CullMode = ConvertCullMode(value);
                _description = desc;
            }
        }

        public Andastra.Runtime.Graphics.FillMode FillMode
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

        private static global::Stride.Graphics.CullMode ConvertCullMode(Andastra.Runtime.Graphics.CullMode mode)
        {
            switch (mode)
            {
                case Andastra.Runtime.Graphics.CullMode.None:
                    return global::Stride.Graphics.CullMode.None;
                case Andastra.Runtime.Graphics.CullMode.CullClockwiseFace:
                    return global::Stride.Graphics.CullMode.Front;
                case Andastra.Runtime.Graphics.CullMode.CullCounterClockwiseFace:
                    return global::Stride.Graphics.CullMode.Back;
                default:
                    return global::Stride.Graphics.CullMode.Back;
            }
        }

        private static Andastra.Runtime.Graphics.CullMode ConvertCullMode(global::Stride.Graphics.CullMode mode)
        {
            switch (mode)
            {
                case global::Stride.Graphics.CullMode.None:
                    return Andastra.Runtime.Graphics.CullMode.None;
                case global::Stride.Graphics.CullMode.Front:
                    return Andastra.Runtime.Graphics.CullMode.CullClockwiseFace;
                case global::Stride.Graphics.CullMode.Back:
                    return Andastra.Runtime.Graphics.CullMode.CullCounterClockwiseFace;
                default:
                    return Andastra.Runtime.Graphics.CullMode.CullCounterClockwiseFace;
            }
        }

        private static StrideGraphics.FillMode ConvertFillMode(Andastra.Runtime.Graphics.FillMode mode)
        {
            switch (mode)
            {
                case Andastra.Runtime.Graphics.FillMode.Solid:
                    return StrideGraphics.FillMode.Solid;
                case Andastra.Runtime.Graphics.FillMode.WireFrame:
                    return StrideGraphics.FillMode.Wireframe;
                default:
                    return StrideGraphics.FillMode.Solid;
            }
        }

        private static Andastra.Runtime.Graphics.FillMode ConvertFillMode(StrideGraphics.FillMode mode)
        {
            switch (mode)
            {
                case StrideGraphics.FillMode.Solid:
                    return Andastra.Runtime.Graphics.FillMode.Solid;
                case StrideGraphics.FillMode.Wireframe:
                    return Andastra.Runtime.Graphics.FillMode.WireFrame;
                default:
                    return Andastra.Runtime.Graphics.FillMode.Solid;
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

        public Andastra.Runtime.Graphics.CompareFunction DepthBufferFunction
        {
            get { return ConvertCompareFunction(_description.DepthBufferFunction); }
            set
            {
                var desc = _description;
                desc.DepthBufferFunction = ConvertCompareFunctionToStride(value);
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

        public Andastra.Runtime.Graphics.StencilOperation StencilFail
        {
            get { return ConvertStencilOperation(_description.FrontFace.StencilFail); }
            set
            {
                var desc = _description;
                var frontFace = desc.FrontFace;
                frontFace.StencilFail = ConvertStencilOperationToStride(value);
                desc.FrontFace = frontFace;
                _description = desc;
            }
        }

        public Andastra.Runtime.Graphics.StencilOperation StencilDepthFail
        {
            get { return ConvertStencilOperation(_description.FrontFace.StencilDepthFail); }
            set
            {
                var desc = _description;
                var frontFace = desc.FrontFace;
                frontFace.StencilDepthFail = ConvertStencilOperationToStride(value);
                desc.FrontFace = frontFace;
                _description = desc;
            }
        }

        public Andastra.Runtime.Graphics.StencilOperation StencilPass
        {
            get { return ConvertStencilOperation(_description.FrontFace.StencilPass); }
            set
            {
                var desc = _description;
                var frontFace = desc.FrontFace;
                frontFace.StencilPass = ConvertStencilOperationToStride(value);
                desc.FrontFace = frontFace;
                _description = desc;
            }
        }

        public Andastra.Runtime.Graphics.CompareFunction StencilFunction
        {
            get { return ConvertCompareFunction(_description.FrontFace.StencilFunction); }
            set
            {
                var desc = _description;
                var frontFace = desc.FrontFace;
                frontFace.StencilFunction = ConvertCompareFunctionToStride(value);
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
            get { return _description.StencilMask; }
            set
            {
                var desc = _description;
                desc.StencilMask = value;
                _description = desc;
            }
        }

        public int StencilWriteMask
        {
            get { return _description.StencilWriteMask; }
            set
            {
                var desc = _description;
                desc.StencilWriteMask = value;
                _description = desc;
            }
        }

        public void Dispose()
        {
            // DepthStencilStateDescription is a struct, nothing to dispose
        }

        internal DepthStencilStateDescription Description => _description;

        private static StrideGraphics.CompareFunction ConvertCompareFunction(Andastra.Runtime.Graphics.CompareFunction func)
        {
            return (StrideGraphics.CompareFunction)(int)func;
        }

        private static Andastra.Runtime.Graphics.CompareFunction ConvertCompareFunction(StrideGraphics.CompareFunction func)
        {
            return (Andastra.Runtime.Graphics.CompareFunction)(int)func;
        }

        /// <summary>
        /// Converts Andastra.Runtime.Stride.Graphics.CompareFunction to Andastra.Runtime.Graphics.CompareFunction.
        /// Both enums have identical values, so a direct cast is appropriate.
        /// </summary>
        private static Andastra.Runtime.Graphics.CompareFunction ConvertCompareFunction(CompareFunction func)
        {
            return (Andastra.Runtime.Graphics.CompareFunction)(int)func;
        }

        /// <summary>
        /// Converts Andastra.Runtime.Graphics.CompareFunction to Andastra.Runtime.Stride.Graphics.CompareFunction.
        /// Both enums have identical values, so a direct cast is appropriate.
        /// </summary>
        private static CompareFunction ConvertCompareFunctionToStride(Andastra.Runtime.Graphics.CompareFunction func)
        {
            return (CompareFunction)(int)func;
        }

        private static StrideGraphics.StencilOperation ConvertStencilOperation(Andastra.Runtime.Graphics.StencilOperation op)
        {
            return (StrideGraphics.StencilOperation)(int)op;
        }

        private static Andastra.Runtime.Graphics.StencilOperation ConvertStencilOperation(StrideGraphics.StencilOperation op)
        {
            return (Andastra.Runtime.Graphics.StencilOperation)(int)op;
        }

        /// <summary>
        /// Converts Andastra.Runtime.Stride.Graphics.StencilOperation to Andastra.Runtime.Graphics.StencilOperation.
        /// Both enums have identical values, so a direct cast is appropriate.
        /// </summary>
        private static Andastra.Runtime.Graphics.StencilOperation ConvertStencilOperation(StencilOperation op)
        {
            return (Andastra.Runtime.Graphics.StencilOperation)(int)op;
        }

        /// <summary>
        /// Converts Andastra.Runtime.Graphics.StencilOperation to Andastra.Runtime.Stride.Graphics.StencilOperation.
        /// Both enums have identical values, so a direct cast is appropriate.
        /// </summary>
        private static StencilOperation ConvertStencilOperationToStride(Andastra.Runtime.Graphics.StencilOperation op)
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
            _description = description ?? BlendStateDescription.Default();
        }

        public Andastra.Runtime.Graphics.BlendFunction AlphaBlendFunction
        {
            get { return ConvertBlendFunction(_description.AlphaBlendFunction); }
            set
            {
                var desc = _description;
                desc.AlphaBlendFunction = ConvertBlendFunctionToStride(value);
                _description = desc;
            }
        }

        public Andastra.Runtime.Graphics.Blend AlphaDestinationBlend
        {
            get { return ConvertBlend(_description.AlphaDestinationBlend); }
            set
            {
                var desc = _description;
                desc.AlphaDestinationBlend = ConvertBlend(value);
                _description = desc;
            }
        }

        public Andastra.Runtime.Graphics.Blend AlphaSourceBlend
        {
            get { return ConvertBlend(_description.AlphaSourceBlend); }
            set
            {
                var desc = _description;
                desc.AlphaSourceBlend = ConvertBlend(value);
                _description = desc;
            }
        }

        public Andastra.Runtime.Graphics.BlendFunction ColorBlendFunction
        {
            get { return ConvertBlendFunction(_description.ColorBlendFunction); }
            set
            {
                var desc = _description;
                desc.ColorBlendFunction = ConvertBlendFunctionToStride(value);
                _description = desc;
            }
        }

        public Andastra.Runtime.Graphics.Blend ColorDestinationBlend
        {
            get { return ConvertBlend(_description.ColorDestinationBlend); }
            set
            {
                var desc = _description;
                desc.ColorDestinationBlend = ConvertBlend(value);
                _description = desc;
            }
        }

        public Andastra.Runtime.Graphics.Blend ColorSourceBlend
        {
            get { return ConvertBlend(_description.ColorSourceBlend); }
            set
            {
                var desc = _description;
                desc.ColorSourceBlend = ConvertBlend(value);
                _description = desc;
            }
        }

        public Andastra.Runtime.Graphics.ColorWriteChannels ColorWriteChannels
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

        public Andastra.Runtime.Graphics.ColorWriteChannels ColorWriteChannels1
        {
            get { return ConvertColorWriteChannels(_description.RenderTargets.Length > 1 ? _description.RenderTargets[1].ColorWriteChannels : StrideGraphics.ColorWriteChannels.None); }
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

        public Andastra.Runtime.Graphics.ColorWriteChannels ColorWriteChannels2
        {
            get { return ConvertColorWriteChannels(_description.RenderTargets.Length > 2 ? _description.RenderTargets[2].ColorWriteChannels : StrideGraphics.ColorWriteChannels.None); }
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

        public Andastra.Runtime.Graphics.ColorWriteChannels ColorWriteChannels3
        {
            get { return ConvertColorWriteChannels(_description.RenderTargets.Length > 3 ? _description.RenderTargets[3].ColorWriteChannels : StrideGraphics.ColorWriteChannels.None); }
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

        public Andastra.Runtime.Graphics.Color BlendFactor
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

        /// <summary>
        /// Converts Andastra BlendFunction to Stride BlendFunction.
        /// Both enums have identical values (Add, Subtract, ReverseSubtract, Min, Max),
        /// so a direct cast is appropriate.
        /// </summary>
        /// <param name="func">Andastra blend function to convert.</param>
        /// <returns>Equivalent Stride blend function.</returns>
        private static StrideGraphics.BlendFunction ConvertBlendFunction(Andastra.Runtime.Graphics.BlendFunction func)
        {
            // Stride's BlendFunction enum matches Andastra's BlendFunction enum exactly:
            // Add = 0, Subtract = 1, ReverseSubtract = 2, Min = 3, Max = 4
            return (StrideGraphics.BlendFunction)(int)func;
        }

        /// <summary>
        /// Converts Stride BlendFunction to Andastra BlendFunction.
        /// Both enums have identical values (Add, Subtract, ReverseSubtract, Min, Max),
        /// so a direct cast is appropriate.
        /// </summary>
        /// <param name="func">Stride blend function to convert.</param>
        /// <returns>Equivalent Andastra blend function.</returns>
        private static Andastra.Runtime.Graphics.BlendFunction ConvertBlendFunction(StrideGraphics.BlendFunction func)
        {
            // Andastra's BlendFunction enum matches Stride's BlendFunction enum exactly:
            // Add = 0, Subtract = 1, ReverseSubtract = 2, Min = 3, Max = 4
            return (Andastra.Runtime.Graphics.BlendFunction)(int)func;
        }

        /// <summary>
        /// Converts Andastra.Runtime.Stride.Graphics.BlendFunction to Andastra.Runtime.Graphics.BlendFunction.
        /// Both enums have identical values (Add, Subtract, ReverseSubtract, Min, Max),
        /// so a direct cast is appropriate.
        /// </summary>
        /// <param name="func">Stride namespace blend function to convert.</param>
        /// <returns>Equivalent Andastra blend function.</returns>
        private static Andastra.Runtime.Graphics.BlendFunction ConvertBlendFunction(BlendFunction func)
        {
            // Both are Andastra enums with identical values:
            // Add = 0, Subtract = 1, ReverseSubtract = 2, Min = 3, Max = 4
            return (Andastra.Runtime.Graphics.BlendFunction)(int)func;
        }

        /// <summary>
        /// Converts Andastra.Runtime.Graphics.BlendFunction to Andastra.Runtime.Stride.Graphics.BlendFunction.
        /// Both enums have identical values (Add, Subtract, ReverseSubtract, Min, Max),
        /// so a direct cast is appropriate.
        /// </summary>
        /// <param name="func">Andastra blend function to convert.</param>
        /// <returns>Equivalent Stride namespace blend function.</returns>
        private static BlendFunction ConvertBlendFunctionToStride(Andastra.Runtime.Graphics.BlendFunction func)
        {
            // Both are Andastra enums with identical values:
            // Add = 0, Subtract = 1, ReverseSubtract = 2, Min = 3, Max = 4
            return (BlendFunction)(int)func;
        }

        private static StrideGraphics.Blend ConvertBlend(Andastra.Runtime.Graphics.Blend blend)
        {
            return (StrideGraphics.Blend)(int)blend;
        }

        private static Andastra.Runtime.Graphics.Blend ConvertBlend(StrideGraphics.Blend blend)
        {
            return (Andastra.Runtime.Graphics.Blend)(int)blend;
        }

        private static StrideGraphics.ColorWriteChannels ConvertColorWriteChannels(Andastra.Runtime.Graphics.ColorWriteChannels channels)
        {
            return (StrideGraphics.ColorWriteChannels)(int)channels;
        }

        private static Andastra.Runtime.Graphics.ColorWriteChannels ConvertColorWriteChannels(StrideGraphics.ColorWriteChannels channels)
        {
            return (Andastra.Runtime.Graphics.ColorWriteChannels)(int)channels;
        }

        private static global::Stride.Core.Mathematics.Color4 ConvertColor(Andastra.Runtime.Graphics.Color color)
        {
            return new global::Stride.Core.Mathematics.Color4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, color.A / 255.0f);
        }

        private static Andastra.Runtime.Graphics.Color ConvertColor(global::Stride.Core.Mathematics.Color4 color)
        {
            return new Andastra.Runtime.Graphics.Color((byte)(color.R * 255), (byte)(color.G * 255), (byte)(color.B * 255), (byte)(color.A * 255));
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

        public Andastra.Runtime.Graphics.TextureAddressMode AddressU
        {
            get { return ConvertTextureAddressMode(_description.AddressU); }
            set
            {
                var desc = _description;
                desc.AddressU = ConvertTextureAddressMode(value);
                _description = desc;
            }
        }

        public Andastra.Runtime.Graphics.TextureAddressMode AddressV
        {
            get { return ConvertTextureAddressMode(_description.AddressV); }
            set
            {
                var desc = _description;
                desc.AddressV = ConvertTextureAddressMode(value);
                _description = desc;
            }
        }

        public Andastra.Runtime.Graphics.TextureAddressMode AddressW
        {
            get { return ConvertTextureAddressMode(_description.AddressW); }
            set
            {
                var desc = _description;
                desc.AddressW = ConvertTextureAddressMode(value);
                _description = desc;
            }
        }

        public Andastra.Runtime.Graphics.TextureFilter Filter
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
            get { return _description.MaxMipLevel; }
            set
            {
                var desc = _description;
                desc.MaxMipLevel = value;
                _description = desc;
            }
        }

        public float MipMapLevelOfDetailBias
        {
            get { return (float)_description.MipMapLevelOfDetailBias; }
            set
            {
                var desc = _description;
                desc.MipMapLevelOfDetailBias = (double)value;
                _description = desc;
            }
        }

        public void Dispose()
        {
            // SamplerStateDescription is a struct, nothing to dispose
        }

        internal SamplerStateDescription Description => _description;

        // Conversion from Andastra.Runtime.Graphics to Andastra.Runtime.Stride.Graphics (for SamplerStateDescription)
        private static TextureAddressMode ConvertTextureAddressMode(Andastra.Runtime.Graphics.TextureAddressMode mode)
        {
            // Both Andastra enums have identical values, so we can cast directly
            return (TextureAddressMode)(int)mode;
        }

        // Conversion from Andastra.Runtime.Stride.Graphics to Andastra.Runtime.Graphics (for public interface)
        private static Andastra.Runtime.Graphics.TextureAddressMode ConvertTextureAddressMode(TextureAddressMode mode)
        {
            // Both Andastra enums have identical values, so we can cast directly
            return (Andastra.Runtime.Graphics.TextureAddressMode)(int)mode;
        }

        // Conversion from Andastra.Runtime.Graphics to Andastra.Runtime.Stride.Graphics (for SamplerStateDescription)
        private static TextureFilter ConvertTextureFilter(Andastra.Runtime.Graphics.TextureFilter filter)
        {
            // Both Andastra enums have identical values, so we can cast directly
            return (TextureFilter)(int)filter;
        }

        // Conversion from Andastra.Runtime.Stride.Graphics to Andastra.Runtime.Graphics (for public interface)
        private static Andastra.Runtime.Graphics.TextureFilter ConvertTextureFilter(TextureFilter filter)
        {
            // Both Andastra enums have identical values, so we can cast directly
            return (Andastra.Runtime.Graphics.TextureFilter)(int)filter;
        }
    }
}


using System;

namespace Andastra.Runtime.Graphics
{
    /// <summary>
    /// Rasterizer state abstraction for controlling how primitives are rasterized.
    /// </summary>
    /// <remarks>
    /// Render State Interfaces:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) DirectX render state system
    /// - Located via string references: Original game uses DirectX 8/9 render states (D3DRS_* constants)
    /// - Depth-stencil: "GL_ARB_depth_texture" @ 0x007b8848, "glDepthMask" @ 0x0080aa38, "glDepthFunc" @ 0x0080ad96
    /// - Stencil: "glStencilOp" @ 0x0080a9f0, "glStencilMask" @ 0x0080aa0c, "glStencilFunc" @ 0x0080aa68
    /// - "glClearStencil" @ 0x0080ada4, "GL_EXT_stencil_two_side" @ 0x007b8a68
    /// - Original implementation: DirectX 8/9 render states control rasterization, depth testing, blending, texture sampling
    /// - Render states: Cull mode, fill mode, depth bias, scissor test, multisampling
    /// - Depth-stencil states: Depth buffer enable/write, depth function, stencil operations
    /// - Blend states: Alpha/color blend functions, blend factors, color write channels
    /// - Sampler states: Texture addressing modes, filtering, anisotropy, mipmap bias
    /// - This interface: Abstraction layer for modern graphics APIs (DirectX 11/12, OpenGL, Vulkan)
    /// </remarks>
    public interface IRasterizerState : IDisposable
    {
        CullMode CullMode { get; set; }
        FillMode FillMode { get; set; }
        bool DepthBiasEnabled { get; set; }
        float DepthBias { get; set; }
        float SlopeScaleDepthBias { get; set; }
        bool ScissorTestEnabled { get; set; }
        bool MultiSampleAntiAlias { get; set; }
    }

    /// <summary>
    /// Depth-stencil state abstraction for controlling depth and stencil testing.
    /// </summary>
    public interface IDepthStencilState : IDisposable
    {
        bool DepthBufferEnable { get; set; }
        bool DepthBufferWriteEnable { get; set; }
        CompareFunction DepthBufferFunction { get; set; }
        bool StencilEnable { get; set; }
        bool TwoSidedStencilMode { get; set; }
        StencilOperation StencilFail { get; set; }
        StencilOperation StencilDepthFail { get; set; }
        StencilOperation StencilPass { get; set; }
        CompareFunction StencilFunction { get; set; }
        int ReferenceStencil { get; set; }
        int StencilMask { get; set; }
        int StencilWriteMask { get; set; }
    }

    /// <summary>
    /// Blend state abstraction for controlling color blending.
    /// </summary>
    public interface IBlendState : IDisposable
    {
        BlendFunction AlphaBlendFunction { get; set; }
        Blend AlphaDestinationBlend { get; set; }
        Blend AlphaSourceBlend { get; set; }
        BlendFunction ColorBlendFunction { get; set; }
        Blend ColorDestinationBlend { get; set; }
        Blend ColorSourceBlend { get; set; }
        ColorWriteChannels ColorWriteChannels { get; set; }
        ColorWriteChannels ColorWriteChannels1 { get; set; }
        ColorWriteChannels ColorWriteChannels2 { get; set; }
        ColorWriteChannels ColorWriteChannels3 { get; set; }
        bool BlendEnable { get; set; }
        Color BlendFactor { get; set; }
        int MultiSampleMask { get; set; }
    }

    /// <summary>
    /// Sampler state abstraction for controlling texture sampling.
    /// </summary>
    public interface ISamplerState : IDisposable
    {
        TextureAddressMode AddressU { get; set; }
        TextureAddressMode AddressV { get; set; }
        TextureAddressMode AddressW { get; set; }
        TextureFilter Filter { get; set; }
        int MaxAnisotropy { get; set; }
        int MaxMipLevel { get; set; }
        float MipMapLevelOfDetailBias { get; set; }
    }

    /// <summary>
    /// Cull mode for face culling.
    /// </summary>
    public enum CullMode
    {
        None,
        CullClockwiseFace,
        CullCounterClockwiseFace
    }

    /// <summary>
    /// Fill mode for primitive rendering.
    /// </summary>
    public enum FillMode
    {
        Solid,
        WireFrame
    }

    /// <summary>
    /// Compare function for depth/stencil testing.
    /// </summary>
    public enum CompareFunction
    {
        Always,
        Never,
        Less,
        LessEqual,
        Equal,
        GreaterEqual,
        Greater,
        NotEqual
    }

    /// <summary>
    /// Stencil operation.
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
    /// Blend function.
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
    /// Blend factor.
    /// </summary>
    public enum Blend
    {
        Zero,
        One,
        SourceColor,
        InverseSourceColor,
        SourceAlpha,
        InverseSourceAlpha,
        DestinationColor,
        InverseDestinationColor,
        DestinationAlpha,
        InverseDestinationAlpha,
        BlendFactor,
        InverseBlendFactor,
        SourceAlphaSaturation
    }

    /// <summary>
    /// Color write channels.
    /// </summary>
    [Flags]
    public enum ColorWriteChannels
    {
        None = 0,
        Red = 1,
        Green = 2,
        Blue = 4,
        Alpha = 8,
        All = Red | Green | Blue | Alpha
    }

    /// <summary>
    /// Texture address mode.
    /// </summary>
    public enum TextureAddressMode
    {
        Wrap,
        Clamp,
        Mirror,
        Border
    }

    /// <summary>
    /// Texture filter mode.
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


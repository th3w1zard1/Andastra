using System;
using System.Numerics;

namespace Andastra.Runtime.Graphics
{
    /// <summary>
    /// Effect (shader) abstraction for 3D rendering.
    /// Provides unified access to shader effects across MonoGame and Stride.
    /// </summary>
    /// <remarks>
    /// Effect (Shader) Interface:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) rendering system
    /// - Located via string references: Original game uses DirectX 8/9 fixed-function pipeline (no shaders)
    /// - Vertex program for skinned animations @ 0x0081c228, 0x0081fe20 (GPU skinning shader)
    /// - Original implementation: DirectX 8/9 fixed-function pipeline, no programmable shaders
    /// - Fixed-function: Uses DirectX fixed-function states (lighting, materials, textures) instead of shaders
    /// - Skinned animation: Uses vertex program (shader) for GPU skinning of animated models
    /// - This interface: Abstraction layer for modern programmable shader pipelines (HLSL, GLSL)
    /// - Note: Modern graphics APIs use programmable shaders, original game primarily used fixed-function
    /// </remarks>
    public interface IEffect : IDisposable
    {
        /// <summary>
        /// Gets the current technique.
        /// </summary>
        IEffectTechnique CurrentTechnique { get; }

        /// <summary>
        /// Gets all techniques in this effect.
        /// </summary>
        IEffectTechnique[] Techniques { get; }
    }

    /// <summary>
    /// Basic effect abstraction for simple 3D rendering (equivalent to MonoGame's BasicEffect).
    /// </summary>
    public interface IBasicEffect : IEffect
    {
        /// <summary>
        /// Gets or sets the world transformation matrix.
        /// </summary>
        Matrix4x4 World { get; set; }

        /// <summary>
        /// Gets or sets the view transformation matrix.
        /// </summary>
        Matrix4x4 View { get; set; }

        /// <summary>
        /// Gets or sets the projection transformation matrix.
        /// </summary>
        Matrix4x4 Projection { get; set; }

        /// <summary>
        /// Gets or sets whether vertex color is enabled.
        /// </summary>
        bool VertexColorEnabled { get; set; }

        /// <summary>
        /// Gets or sets whether lighting is enabled.
        /// </summary>
        bool LightingEnabled { get; set; }

        /// <summary>
        /// Gets or sets whether texture is enabled.
        /// </summary>
        bool TextureEnabled { get; set; }

        /// <summary>
        /// Gets or sets the ambient light color.
        /// </summary>
        Vector3 AmbientLightColor { get; set; }

        /// <summary>
        /// Gets or sets the diffuse color.
        /// </summary>
        Vector3 DiffuseColor { get; set; }

        /// <summary>
        /// Gets or sets the emissive color.
        /// </summary>
        Vector3 EmissiveColor { get; set; }

        /// <summary>
        /// Gets or sets the specular color.
        /// </summary>
        Vector3 SpecularColor { get; set; }

        /// <summary>
        /// Gets or sets the specular power.
        /// </summary>
        float SpecularPower { get; set; }

        /// <summary>
        /// Gets or sets the alpha (transparency).
        /// </summary>
        float Alpha { get; set; }

        /// <summary>
        /// Gets or sets the texture.
        /// </summary>
        ITexture2D Texture { get; set; }

        /// <summary>
        /// Gets or sets whether fog is enabled.
        /// </summary>
        bool FogEnabled { get; set; }

        /// <summary>
        /// Gets or sets the fog color.
        /// </summary>
        Vector3 FogColor { get; set; }

        /// <summary>
        /// Gets or sets the fog start distance (for linear fog).
        /// </summary>
        float FogStart { get; set; }

        /// <summary>
        /// Gets or sets the fog end distance (for linear fog).
        /// </summary>
        float FogEnd { get; set; }

        /// <summary>
        /// Applies the effect to the graphics device.
        /// This applies all passes of the current technique.
        /// </summary>
        void Apply();
    }

    /// <summary>
    /// Effect technique abstraction.
    /// </summary>
    public interface IEffectTechnique
    {
        /// <summary>
        /// Gets the name of the technique.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets all passes in this technique.
        /// </summary>
        IEffectPass[] Passes { get; }
    }

    /// <summary>
    /// Effect pass abstraction.
    /// </summary>
    public interface IEffectPass
    {
        /// <summary>
        /// Gets the name of the pass.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Applies the effect pass to the graphics device.
        /// </summary>
        void Apply();
    }
}


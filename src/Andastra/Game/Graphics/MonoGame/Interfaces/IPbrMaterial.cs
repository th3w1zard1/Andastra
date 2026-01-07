using System;
using System.Numerics;
using Andastra.Runtime.MonoGame.Enums;

namespace Andastra.Runtime.MonoGame.Interfaces
{
    /// <summary>
    /// Physically-based rendering material interface.
    /// </summary>
    public interface IPbrMaterial : IDisposable
    {
        /// <summary>
        /// Material name/identifier.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Material type (opaque, transparent, etc).
        /// </summary>
        MaterialType Type { get; set; }

        /// <summary>
        /// Which texture channels are active.
        /// </summary>
        TextureChannels ActiveChannels { get; }

        #region Base Color

        /// <summary>
        /// Base color (albedo) texture.
        /// </summary>
        IntPtr AlbedoTexture { get; set; }

        /// <summary>
        /// Base color tint (multiplied with texture).
        /// </summary>
        Vector4 AlbedoColor { get; set; }

        /// <summary>
        /// UV scale for albedo texture.
        /// </summary>
        Vector2 AlbedoUvScale { get; set; }

        /// <summary>
        /// UV offset for albedo texture.
        /// </summary>
        Vector2 AlbedoUvOffset { get; set; }

        #endregion

        #region Normal Mapping

        /// <summary>
        /// Normal map texture (tangent space).
        /// </summary>
        IntPtr NormalTexture { get; set; }

        /// <summary>
        /// Normal map intensity (0-2).
        /// </summary>
        float NormalIntensity { get; set; }

        /// <summary>
        /// Whether to flip Y channel (for DirectX vs OpenGL normals).
        /// </summary>
        bool NormalFlipY { get; set; }

        #endregion

        #region PBR Properties

        /// <summary>
        /// Metallic texture (grayscale).
        /// </summary>
        IntPtr MetallicTexture { get; set; }

        /// <summary>
        /// Metallic value (0-1) when no texture.
        /// </summary>
        float Metallic { get; set; }

        /// <summary>
        /// Roughness texture (grayscale).
        /// </summary>
        IntPtr RoughnessTexture { get; set; }

        /// <summary>
        /// Roughness value (0-1) when no texture.
        /// </summary>
        float Roughness { get; set; }

        /// <summary>
        /// Combined ORM texture (Occlusion, Roughness, Metallic in RGB).
        /// </summary>
        IntPtr OrmTexture { get; set; }

        /// <summary>
        /// Ambient occlusion texture.
        /// </summary>
        IntPtr AoTexture { get; set; }

        /// <summary>
        /// Ambient occlusion intensity.
        /// </summary>
        float AoIntensity { get; set; }

        #endregion

        #region Emission

        /// <summary>
        /// Emissive texture (RGB color).
        /// </summary>
        IntPtr EmissiveTexture { get; set; }

        /// <summary>
        /// Emissive color (when no texture).
        /// </summary>
        Vector3 EmissiveColor { get; set; }

        /// <summary>
        /// Emissive intensity multiplier.
        /// </summary>
        float EmissiveIntensity { get; set; }

        #endregion

        #region Displacement

        /// <summary>
        /// Height/displacement texture.
        /// </summary>
        IntPtr HeightTexture { get; set; }

        /// <summary>
        /// Parallax occlusion mapping intensity.
        /// </summary>
        float HeightIntensity { get; set; }

        /// <summary>
        /// Number of POM layers.
        /// </summary>
        int HeightLayers { get; set; }

        #endregion

        #region Lightmap

        /// <summary>
        /// Lightmap texture (baked lighting).
        /// </summary>
        IntPtr LightmapTexture { get; set; }

        /// <summary>
        /// Lightmap UV channel (typically 1).
        /// </summary>
        int LightmapUvChannel { get; set; }

        /// <summary>
        /// Lightmap intensity multiplier.
        /// </summary>
        float LightmapIntensity { get; set; }

        #endregion

        #region Environment

        /// <summary>
        /// Environment/reflection cubemap.
        /// </summary>
        IntPtr EnvironmentTexture { get; set; }

        /// <summary>
        /// Environment reflection intensity.
        /// </summary>
        float EnvironmentIntensity { get; set; }

        #endregion

        #region Transparency

        /// <summary>
        /// Alpha cutoff threshold (for alpha test).
        /// </summary>
        float AlphaCutoff { get; set; }

        /// <summary>
        /// Overall opacity (0-1).
        /// </summary>
        float Opacity { get; set; }

        /// <summary>
        /// Index of refraction (for glass/water).
        /// </summary>
        float IndexOfRefraction { get; set; }

        /// <summary>
        /// Refraction enabled.
        /// </summary>
        bool RefractionEnabled { get; set; }

        #endregion

        #region Subsurface Scattering

        /// <summary>
        /// Subsurface scattering color.
        /// </summary>
        Vector3 SubsurfaceColor { get; set; }

        /// <summary>
        /// Subsurface scattering radius.
        /// </summary>
        float SubsurfaceRadius { get; set; }

        /// <summary>
        /// Subsurface thickness texture.
        /// </summary>
        IntPtr SubsurfaceTexture { get; set; }

        #endregion

        #region Clear Coat

        /// <summary>
        /// Clear coat intensity (0-1).
        /// </summary>
        float ClearCoat { get; set; }

        /// <summary>
        /// Clear coat roughness.
        /// </summary>
        float ClearCoatRoughness { get; set; }

        /// <summary>
        /// Clear coat normal map.
        /// </summary>
        IntPtr ClearCoatNormalTexture { get; set; }

        #endregion

        #region Anisotropy

        /// <summary>
        /// Anisotropy strength (-1 to 1).
        /// </summary>
        float Anisotropy { get; set; }

        /// <summary>
        /// Anisotropy rotation (0-1 maps to 0-360 degrees).
        /// </summary>
        float AnisotropyRotation { get; set; }

        /// <summary>
        /// Anisotropy direction texture.
        /// </summary>
        IntPtr AnisotropyTexture { get; set; }

        #endregion

        #region Detail Textures

        /// <summary>
        /// Detail albedo texture (tiled).
        /// </summary>
        IntPtr DetailAlbedoTexture { get; set; }

        /// <summary>
        /// Detail normal texture (tiled).
        /// </summary>
        IntPtr DetailNormalTexture { get; set; }

        /// <summary>
        /// Detail texture UV scale.
        /// </summary>
        Vector2 DetailUvScale { get; set; }

        /// <summary>
        /// Detail blend mask texture.
        /// </summary>
        IntPtr DetailMaskTexture { get; set; }

        #endregion

        #region Rendering State

        /// <summary>
        /// Enable double-sided rendering.
        /// </summary>
        bool DoubleSided { get; set; }

        /// <summary>
        /// Render queue/priority.
        /// </summary>
        int RenderQueue { get; set; }

        /// <summary>
        /// Cast shadows.
        /// </summary>
        bool CastShadows { get; set; }

        /// <summary>
        /// Receive shadows.
        /// </summary>
        bool ReceiveShadows { get; set; }

        #endregion

        /// <summary>
        /// Binds the material for rendering.
        /// </summary>
        void Bind();

        /// <summary>
        /// Updates material parameters to GPU.
        /// </summary>
        void UpdateParameters();

        /// <summary>
        /// Creates a copy of this material.
        /// </summary>
        IPbrMaterial Clone();
    }

    /// <summary>
    /// Factory for creating PBR materials.
    /// </summary>
    public interface IPbrMaterialFactory
    {
        /// <summary>
        /// Creates a new PBR material.
        /// </summary>
        IPbrMaterial Create(string name, MaterialType type);

        /// <summary>
        /// Creates a material from KOTOR MDL material data.
        /// </summary>
        IPbrMaterial CreateFromKotorMaterial(string name, KotorMaterialData data);

        /// <summary>
        /// Gets a cached material by name.
        /// </summary>
        IPbrMaterial GetCached(string name);

        /// <summary>
        /// Preloads all materials for a module.
        /// </summary>
        void PreloadModule(string moduleName);
    }

    /// <summary>
    /// KOTOR material data for conversion to PBR.
    /// </summary>
    public struct KotorMaterialData
    {
        /// <summary>
        /// Diffuse texture name.
        /// </summary>
        public string DiffuseMap;

        /// <summary>
        /// Lightmap texture name.
        /// </summary>
        public string LightmapMap;

        /// <summary>
        /// Environment map texture name.
        /// </summary>
        public string EnvironmentMap;

        /// <summary>
        /// Bump map texture name.
        /// </summary>
        public string BumpMap;

        /// <summary>
        /// Diffuse color.
        /// </summary>
        public Vector4 DiffuseColor;

        /// <summary>
        /// Ambient color.
        /// </summary>
        public Vector3 AmbientColor;

        /// <summary>
        /// Specular color.
        /// </summary>
        public Vector3 SpecularColor;

        /// <summary>
        /// Specular power (shininess).
        /// </summary>
        public float SpecularPower;

        /// <summary>
        /// Self-illumination color.
        /// </summary>
        public Vector3 SelfIllumColor;

        /// <summary>
        /// Alpha value (transparency).
        /// </summary>
        public float Alpha;

        /// <summary>
        /// Texture animation data.
        /// </summary>
        public TextureAnimationData Animation;

        /// <summary>
        /// Render hints from MDL.
        /// </summary>
        public KotorRenderHints RenderHints;
    }

    /// <summary>
    /// Texture animation parameters.
    /// </summary>
    public struct TextureAnimationData
    {
        public bool Animated;
        public float ScrollX;
        public float ScrollY;
        public float RotateSpeed;
        public int FrameCount;
        public float FrameRate;
    }

    /// <summary>
    /// KOTOR-specific render hints from MDL.
    /// </summary>
    [Flags]
    public enum KotorRenderHints
    {
        None = 0,
        Transparent = 1 << 0,
        Additive = 1 << 1,
        Decal = 1 << 2,
        TwoSided = 1 << 3,
        NoShadow = 1 << 4,
        Hologram = 1 << 5,
        DanglyMesh = 1 << 6,
        Skin = 1 << 7,
        AABB = 1 << 8,
        Saber = 1 << 9
    }
}


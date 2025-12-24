using System;

namespace Andastra.Runtime.MonoGame.Enums
{
    /// <summary>
    /// PBR material types supported by the Odyssey renderer.
    /// </summary>
    public enum MaterialType
    {
        /// <summary>
        /// Standard opaque PBR material.
        /// </summary>
        Opaque,

        /// <summary>
        /// Alpha-tested cutout material (foliage, grates).
        /// </summary>
        AlphaCutout,

        /// <summary>
        /// Alpha-blended transparent material.
        /// </summary>
        AlphaBlend,

        /// <summary>
        /// Additive blending (energy effects, lightsabers).
        /// </summary>
        Additive,

        /// <summary>
        /// Lightmapped opaque material (static geometry).
        /// </summary>
        LightmappedOpaque,

        /// <summary>
        /// Lightmapped alpha-cutout material.
        /// </summary>
        LightmappedCutout,

        /// <summary>
        /// Emissive self-illuminated material.
        /// </summary>
        Emissive,

        /// <summary>
        /// Skin/subsurface scattering material.
        /// </summary>
        Subsurface,

        /// <summary>
        /// Clear coat material (polished surfaces).
        /// </summary>
        ClearCoat,

        /// <summary>
        /// Holographic projection material.
        /// </summary>
        Hologram,

        /// <summary>
        /// Force field / energy shield material.
        /// </summary>
        ForceField,

        /// <summary>
        /// Water / liquid surface material.
        /// </summary>
        Water,

        /// <summary>
        /// Glass / refractive material.
        /// </summary>
        Glass,

        /// <summary>
        /// Unlit material (UI elements, skybox).
        /// </summary>
        Unlit
    }

    /// <summary>
    /// Texture channel types for PBR materials.
    /// </summary>
    [Flags]
    public enum TextureChannels
    {
        None = 0,

        /// <summary>
        /// Base color / albedo (RGB) + optional alpha (A).
        /// </summary>
        Albedo = 1 << 0,

        /// <summary>
        /// Normal map (tangent space).
        /// </summary>
        Normal = 1 << 1,

        /// <summary>
        /// Metallic value (grayscale).
        /// </summary>
        Metallic = 1 << 2,

        /// <summary>
        /// Roughness value (grayscale).
        /// </summary>
        Roughness = 1 << 3,

        /// <summary>
        /// Ambient occlusion (grayscale).
        /// </summary>
        AmbientOcclusion = 1 << 4,

        /// <summary>
        /// Emissive map (RGB).
        /// </summary>
        Emissive = 1 << 5,

        /// <summary>
        /// Height / displacement map (grayscale).
        /// </summary>
        Height = 1 << 6,

        /// <summary>
        /// Lightmap (baked lighting).
        /// </summary>
        Lightmap = 1 << 7,

        /// <summary>
        /// Environment reflection cubemap.
        /// </summary>
        Environment = 1 << 8,

        /// <summary>
        /// Combined ORM (Occlusion, Roughness, Metallic).
        /// </summary>
        ORM = 1 << 9,

        /// <summary>
        /// Subsurface scattering thickness map.
        /// </summary>
        Subsurface = 1 << 10,

        /// <summary>
        /// Detail albedo (tiled detail texture).
        /// </summary>
        DetailAlbedo = 1 << 11,

        /// <summary>
        /// Detail normal map.
        /// </summary>
        DetailNormal = 1 << 12
    }

    /// <summary>
    /// Light types supported by the renderer.
    /// </summary>
    public enum LightType
    {
        /// <summary>
        /// Directional light (sun, moon).
        /// </summary>
        Directional,

        /// <summary>
        /// Point light (omnidirectional).
        /// </summary>
        Point,

        /// <summary>
        /// Spot light (cone).
        /// </summary>
        Spot,

        /// <summary>
        /// Area light (rectangle or disc).
        /// </summary>
        Area,

        /// <summary>
        /// Capsule/tube light.
        /// </summary>
        Capsule,

        /// <summary>
        /// Image-based lighting (IBL) probe.
        /// </summary>
        Probe
    }
}


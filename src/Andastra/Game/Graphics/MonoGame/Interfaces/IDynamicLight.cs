using System;
using System.Numerics;
using Andastra.Runtime.MonoGame.Enums;

namespace Andastra.Runtime.MonoGame.Interfaces
{
    /// <summary>
    /// Dynamic light source interface.
    /// </summary>
    public interface IDynamicLight : IDisposable
    {
        /// <summary>
        /// Light identifier.
        /// </summary>
        uint LightId { get; }

        /// <summary>
        /// Light type (directional, point, spot, area).
        /// </summary>
        LightType Type { get; }

        /// <summary>
        /// Whether the light is enabled.
        /// </summary>
        bool Enabled { get; set; }

        /// <summary>
        /// Light position in world space.
        /// </summary>
        Vector3 Position { get; set; }

        /// <summary>
        /// Light direction (for directional/spot lights).
        /// </summary>
        Vector3 Direction { get; set; }

        /// <summary>
        /// Light color (linear RGB).
        /// </summary>
        Vector3 Color { get; set; }

        /// <summary>
        /// Light intensity in lumens (point/spot) or lux (directional).
        /// </summary>
        float Intensity { get; set; }

        /// <summary>
        /// Attenuation radius for point/spot lights.
        /// </summary>
        float Radius { get; set; }

        /// <summary>
        /// Inner cone angle in degrees (for spot lights).
        /// </summary>
        float InnerConeAngle { get; set; }

        /// <summary>
        /// Outer cone angle in degrees (for spot lights).
        /// </summary>
        float OuterConeAngle { get; set; }

        /// <summary>
        /// Area light width (for area lights).
        /// </summary>
        float AreaWidth { get; set; }

        /// <summary>
        /// Area light height (for area lights).
        /// </summary>
        float AreaHeight { get; set; }

        /// <summary>
        /// Whether this light casts shadows.
        /// </summary>
        bool CastShadows { get; set; }

        /// <summary>
        /// Shadow map resolution (e.g., 512, 1024, 2048).
        /// </summary>
        int ShadowResolution { get; set; }

        /// <summary>
        /// Shadow bias to prevent shadow acne.
        /// </summary>
        float ShadowBias { get; set; }

        /// <summary>
        /// Shadow normal bias.
        /// </summary>
        float ShadowNormalBias { get; set; }

        /// <summary>
        /// Shadow near plane.
        /// </summary>
        float ShadowNearPlane { get; set; }

        /// <summary>
        /// Shadow softness / penumbra.
        /// </summary>
        float ShadowSoftness { get; set; }

        /// <summary>
        /// Use raytraced shadows (if available).
        /// </summary>
        bool RaytracedShadows { get; set; }

        /// <summary>
        /// Volumetric light contribution.
        /// </summary>
        bool Volumetric { get; set; }

        /// <summary>
        /// Volumetric scattering intensity.
        /// </summary>
        float VolumetricIntensity { get; set; }

        /// <summary>
        /// Light temperature in Kelvin (for color calculation).
        /// </summary>
        float Temperature { get; set; }

        /// <summary>
        /// Use temperature instead of explicit color.
        /// </summary>
        bool UseTemperature { get; set; }

        /// <summary>
        /// IES light profile texture (for photometric lights).
        /// </summary>
        IntPtr IesProfile { get; set; }

        /// <summary>
        /// Light cookie/gobo texture (for pattern projection).
        /// </summary>
        IntPtr CookieTexture { get; set; }

        /// <summary>
        /// Affects specular highlights.
        /// </summary>
        bool AffectsSpecular { get; set; }

        /// <summary>
        /// Culling mask (which layers this light affects).
        /// </summary>
        uint CullingMask { get; set; }

        /// <summary>
        /// Shadow light space matrix (view * projection matrix for shadow mapping).
        /// </summary>
        Matrix4x4 ShadowLightSpaceMatrix { get; set; }

        /// <summary>
        /// Updates the light's transform.
        /// </summary>
        void UpdateTransform(Matrix4x4 worldMatrix);

        /// <summary>
        /// Updates light data to GPU.
        /// </summary>
        void UpdateGpuData();
    }

    /// <summary>
    /// Dynamic lighting system manager.
    /// </summary>
    public interface ILightingSystem : IDisposable
    {
        /// <summary>
        /// Maximum supported dynamic lights.
        /// </summary>
        int MaxLights { get; }

        /// <summary>
        /// Current active light count.
        /// </summary>
        int ActiveLightCount { get; }

        /// <summary>
        /// Ambient light color.
        /// </summary>
        Vector3 AmbientColor { get; set; }

        /// <summary>
        /// Ambient light intensity.
        /// </summary>
        float AmbientIntensity { get; set; }

        /// <summary>
        /// Sky light / environment probe intensity.
        /// </summary>
        float SkyLightIntensity { get; set; }

        /// <summary>
        /// Creates a new dynamic light.
        /// </summary>
        IDynamicLight CreateLight(LightType type);

        /// <summary>
        /// Removes a light from the system.
        /// </summary>
        void RemoveLight(IDynamicLight light);

        /// <summary>
        /// Gets the primary directional light (sun/moon).
        /// </summary>
        IDynamicLight PrimaryDirectionalLight { get; }

        /// <summary>
        /// Sets the primary directional light.
        /// </summary>
        void SetPrimaryDirectionalLight(IDynamicLight light);

        /// <summary>
        /// Gets all active lights.
        /// </summary>
        IDynamicLight[] GetActiveLights();

        /// <summary>
        /// Gets lights affecting a specific point.
        /// </summary>
        IDynamicLight[] GetLightsAffectingPoint(Vector3 position, float radius);

        /// <summary>
        /// Updates the light clustering/tiling for the current view.
        /// </summary>
        void UpdateClustering(Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix);

        /// <summary>
        /// Submits light data for rendering.
        /// </summary>
        void SubmitLightData();

        /// <summary>
        /// Sets global illumination probe data.
        /// </summary>
        void SetGlobalIlluminationProbe(IntPtr probeTexture, float intensity);

        /// <summary>
        /// Sets fog parameters.
        /// </summary>
        void SetFog(FogSettings fog);

        /// <summary>
        /// Gets current fog settings.
        /// </summary>
        FogSettings GetFog();
    }

    /// <summary>
    /// Fog configuration.
    /// </summary>
    public struct FogSettings
    {
        /// <summary>
        /// Fog enabled.
        /// </summary>
        public bool Enabled;

        /// <summary>
        /// Fog mode.
        /// </summary>
        public FogMode Mode;

        /// <summary>
        /// Fog color.
        /// </summary>
        public Vector3 Color;

        /// <summary>
        /// Fog density (for exponential modes).
        /// </summary>
        public float Density;

        /// <summary>
        /// Fog start distance (for linear mode).
        /// </summary>
        public float Start;

        /// <summary>
        /// Fog end distance (for linear mode).
        /// </summary>
        public float End;

        /// <summary>
        /// Height fog enabled.
        /// </summary>
        public bool HeightFog;

        /// <summary>
        /// Height fog base altitude.
        /// </summary>
        public float HeightFogBase;

        /// <summary>
        /// Height fog falloff rate.
        /// </summary>
        public float HeightFogFalloff;

        /// <summary>
        /// Volumetric fog enabled.
        /// </summary>
        public bool Volumetric;

        /// <summary>
        /// Volumetric fog scattering.
        /// </summary>
        public float VolumetricScattering;

        /// <summary>
        /// Inscattering color (from sun/light).
        /// </summary>
        public Vector3 InscatteringColor;
    }

    /// <summary>
    /// Fog modes.
    /// </summary>
    public enum FogMode
    {
        Linear,
        Exponential,
        ExponentialSquared
    }
}

